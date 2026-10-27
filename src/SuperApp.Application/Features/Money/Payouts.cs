using FluentValidation;
using SuperApp.Application.Abstractions.Documents;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Agents;
using SuperApp.Application.Features.Bookings;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Money;

public sealed record PayoutRequestBody(string? WalletType, decimal Amount);

public sealed record PayoutSendBody(string TransferRef);

public sealed record PayoutRejectBody(string Reason);

public sealed record ApprovalDecisionBody(string Decision, string? Note);

public sealed record PayoutDto(string Code, string WalletType, decimal Amount, string Method, string Destination, string Status,
    string? TransferRef, string? RejectReason, DateTime CreatedAt, DateTime? SentAt, string? OwnerName = null, long? ApprovalId = null);

public sealed record ApprovalDto(long Id, string Kind, string RefType, string? RefCode, decimal Amount, string Summary, string Status, DateTime CreatedAt,
    string? RequestedBy = null, bool Mine = false, DateTime? DecidedAt = null, IReadOnlyList<ApprovalDecisionDto>? Decisions = null);

/// <summary>Who decided, what and when (the note is the reason of a rejection).</summary>
public sealed record ApprovalDecisionDto(string Name, string Decision, string? Note, DateTime At);

/// <summary>Who gets paid and where: the partner's payout method and whether the account may withdraw.</summary>
public sealed record PayeeInfo(Guid UserId, string Name, PayoutMethod Method, string Destination, bool Active);

/// <summary>Payout and approval tables.</summary>
public interface IPayoutStore
{
    Task<PayeeInfo?> PayeeAsync(Guid userId, WalletType type, CancellationToken ct);

    Task<string> NewCodeAsync(CancellationToken ct);

    void Add(PayoutRequest payout);

    void Add(Approval approval);

    Task<bool> HasOpenAsync(long walletId, CancellationToken ct);

    Task<PayoutRequest?> FindAsync(string code, CancellationToken ct);

    Task<PayoutRequest?> FindByIdAsync(long id, CancellationToken ct);

    Task<IReadOnlyList<PayoutDto>> MineAsync(Guid userId, CancellationToken ct);

    Task<PagedResult<PayoutDto>> ListAsync(PayoutStatus? status, int page, int pageSize, CancellationToken ct);

    Task<Approval?> ApprovalAsync(long id, CancellationToken ct);

    /// <summary>Newest first (waiting: oldest first); null status = all. <c>Mine</c> marks the ones <paramref name="me"/> asked for.</summary>
    Task<IReadOnlyList<ApprovalDto>> ApprovalsAsync(ApprovalStatus? status, Guid me, CancellationToken ct);

    Task<decimal?> AgentMoneyLimitAsync(long agentId, CancellationToken ct);

    Task<IReadOnlyList<LedgerRowDto>> StatementAsync(Guid userId, WalletType type, CancellationToken ct);
}

/// <summary>
/// Withdrawals (API §5.2). The owner asks (min 500 EGP, up to the available balance, a payout method, an active account,
/// one open request per wallet); the money leaves Available at once. Finance prepares (payouts.prepare); over the approval
/// limit it waits for a finance approver (finance.approve, never the person who prepared it); a different person sends
/// it with the bank / InstaPay reference (payouts.send) → payout advice PDF. A reject puts the money back.
/// </summary>
public sealed class PayoutsService(
    IPayoutStore payouts,
    IMoneyStore money,
    ISettingsReader settings,
    IFeatureFlags flags,
    INotifier notifier,
    IAuditLogger audit,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ICurrentAgent currentAgent,
    IClock clock,
    IExcelExporter excel)
{
    public async Task<PayoutDto> RequestAsync(PayoutRequestBody body, CancellationToken ct)
    {
        await flags.EnsureOnAsync(KillSwitches.Payouts, ct);
        var me = Me();
        var type = OwnType(body.WalletType);
        var min = await settings.DecimalAsync("money.payoutMin", 500m, ct);
        var payee = await payouts.PayeeAsync(me, type, ct) ?? throw new BusinessRuleException("Only partners can withdraw for now.", "no_payout_account");
        var wallet = await money.WalletAsync(me, type, ct);
        Check(body.Amount >= min, $"The smallest withdrawal is {min:0} EGP.", "under_minimum");
        Check(body.Amount <= wallet.AvailableBalance, "That is more than your available balance.", "over_available");
        Check(payee.Active, "Your account must be active to withdraw.", "account_not_active");
        Check(!string.IsNullOrWhiteSpace(payee.Destination), "Add your bank or InstaPay details first.", "no_payout_method");
        if (wallet.Id != 0 && await payouts.HasOpenAsync(wallet.Id, ct))
        {
            throw new ConflictException("You already have a withdrawal in progress.", "payout_open");
        }
        var payout = new PayoutRequest
        {
            Code = await payouts.NewCodeAsync(ct), Wallet = wallet, Amount = body.Amount, Method = payee.Method,
            Destination = payee.Destination, Status = PayoutStatus.Requested,
        };
        payouts.Add(payout);
        var entry = Move(wallet, -body.Amount, LedgerKind.Payout, payout);
        await unitOfWork.SaveChangesAsync(ct);
        entry.RefId = payout.Id;
        await unitOfWork.SaveChangesAsync(ct);
        await notifier.NotifyStaffAsync("payouts.prepare", new NotificationMessage(NotificationCategory.Money,
            $"Withdrawal {payout.Code}", $"{payee.Name} · {payout.Amount:0} EGP", "#/admin/payouts", NotificationTone.Info), ct);
        await audit.LogAsync(AuditSystem.Partner, "payout.requested", $"Withdrawal {payout.Code} of {payout.Amount:0} EGP", "Payout", payout.Code, ct: ct);
        return ToDto(payout, type);
    }

    public Task<IReadOnlyList<PayoutDto>> MineAsync(CancellationToken ct) => payouts.MineAsync(Me(), ct);

    public Task<PagedResult<PayoutDto>> ListAsync(string? status, int page, int pageSize, CancellationToken ct) =>
        payouts.ListAsync(Enum.TryParse<PayoutStatus>(status, true, out var s) ? s : null, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), ct);

    public async Task<PayoutDto> PrepareAsync(string code, CancellationToken ct)
    {
        var payout = await StatusAsync(code, PayoutStatus.Requested, ct);
        payout.PreparedById = Me();
        var limit = await settings.DecimalAsync("money.payoutApprovalOver", 20000m, ct);
        payout.Status = payout.Amount > limit ? PayoutStatus.WaitingForApproval : PayoutStatus.Prepared;
        if (payout.Status == PayoutStatus.WaitingForApproval)
        {
            payout.Approval = new Approval { Kind = ApprovalKind.Payout, RefType = "Payout", RefId = 0, Amount = payout.Amount,
                Summary = $"Withdrawal {payout.Code} of {payout.Amount:0} EGP", RequestedById = Me(), Status = ApprovalStatus.Waiting };
            payouts.Add(payout.Approval);
        }
        await unitOfWork.SaveChangesAsync(ct);
        if (payout.Approval is { } approval)
        {
            approval.RefId = payout.Id;
            await unitOfWork.SaveChangesAsync(ct);
            await notifier.NotifyStaffAsync("finance.approve", new NotificationMessage(NotificationCategory.Money, "Approval needed",
                approval.Summary, "#/ceo/approvals", NotificationTone.Warning), ct);
        }
        await audit.LogAsync(AuditSystem.Admin, "payout.prepared", $"Withdrawal {payout.Code} prepared", "Payout", payout.Code, ct: ct);
        return ToDto(payout, payout.Wallet.Type);
    }

    public async Task<PayoutDto> SendAsync(string code, PayoutSendBody body, CancellationToken ct)
    {
        Check(!string.IsNullOrWhiteSpace(body.TransferRef) && body.TransferRef.Length <= 100, "Enter the bank or InstaPay reference.", "transfer_ref");
        var payout = await StatusAsync(code, PayoutStatus.Prepared, ct);
        var me = Me();
        if (payout.PreparedById == me)
        {
            throw new ConflictException("Another person must send a withdrawal you prepared.", "two_people");
        }
        if (currentAgent.Caller is { } agent && payout.Amount > (await payouts.AgentMoneyLimitAsync(agent.AgentId, ct) ?? 0))
        {
            throw new ForbiddenException("This amount is over the agent's money limit; a person must send it.", "over_money_limit");
        }
        payout.Status = PayoutStatus.Sent;
        payout.SentById = me;
        payout.SentAt = clock.UtcNow;
        payout.TransferRef = body.TransferRef.Trim();
        payout.Wallet.SettledTotal += payout.Amount;
        var owner = payout.Wallet.OwnerUserId!.Value;
        money.Add(new Receipt { Number = await money.NewReceiptNumberAsync(ReceiptKind.PayoutAdvice, ct), UserId = owner, Kind = ReceiptKind.PayoutAdvice,
            Amount = payout.Amount, RefType = "Payout", RefId = payout.Id });
        await unitOfWork.SaveChangesAsync(ct);
        await notifier.NotifyUserAsync(owner, new NotificationMessage(NotificationCategory.Money, "Your withdrawal was sent",
            $"{payout.Code} · {payout.Amount:0} EGP · ref {payout.TransferRef}", "#/dash/wallet", NotificationTone.Success), ct);
        await audit.LogAsync(AuditSystem.Admin, "payout.sent", $"Withdrawal {payout.Code} sent ({payout.TransferRef})", "Payout", payout.Code, ct: ct);
        return ToDto(payout, payout.Wallet.Type);
    }

    public async Task<PayoutDto> RejectAsync(string code, PayoutRejectBody body, CancellationToken ct)
    {
        Check(!string.IsNullOrWhiteSpace(body.Reason) && body.Reason.Length <= 300, "Give a reason (up to 300 characters).", "reason");
        var payout = await payouts.FindAsync(code, ct) ?? throw new NotFoundException("Withdrawal not found.");
        Check(payout.Status is PayoutStatus.Requested or PayoutStatus.Prepared or PayoutStatus.WaitingForApproval, "This withdrawal is already closed.", "payout_closed");
        await CloseRejectedAsync(payout, body.Reason.Trim(), ct);
        return ToDto(payout, payout.Wallet.Type);
    }

    /// <summary>?status=waiting (default) | approved | rejected | all.</summary>
    public Task<IReadOnlyList<ApprovalDto>> ApprovalsAsync(string? status, CancellationToken ct)
    {
        ApprovalStatus? s = status?.ToLowerInvariant() switch
        {
            null or "" or "waiting" => ApprovalStatus.Waiting,
            "approved" => ApprovalStatus.Approved,
            "rejected" => ApprovalStatus.Rejected,
            "all" => null,
            _ => throw new BadRequestException("Status: waiting, approved, rejected or all.", "status"),
        };
        return payouts.ApprovalsAsync(s, Me(), ct);
    }

    /// <summary>Approve → the withdrawal can be sent; reject → it closes and the money goes back. Never your own request.</summary>
    public async Task<ApprovalDto> DecideAsync(long id, ApprovalDecisionBody body, CancellationToken ct)
    {
        var approval = await payouts.ApprovalAsync(id, ct) ?? throw new NotFoundException("Approval not found.");
        Check(approval.Status == ApprovalStatus.Waiting, "This was already decided.", "already_decided");
        var me = Me();
        if (approval.RequestedById == me)
        {
            throw new ConflictException("Another person must approve a request you made.", "two_people");
        }
        var approve = string.Equals(body.Decision, "approve", StringComparison.OrdinalIgnoreCase);
        Check(approve || string.Equals(body.Decision, "reject", StringComparison.OrdinalIgnoreCase), "Decision: approve or reject.", "decision");
        Check(approve || body.Note?.Trim().Length >= 3, "Write the reason — whoever asked sees it.", "note");
        approval.Decisions.Add(new ApprovalDecision { DeciderId = me, Decision = approve ? Decision.Approve : Decision.Reject, Note = body.Note?.Trim() });
        approval.Status = approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        approval.DecidedAt = clock.UtcNow;
        var payout = approval.Kind == ApprovalKind.Payout ? await payouts.FindByIdAsync(approval.RefId, ct) : null;
        if (payout is { Status: PayoutStatus.WaitingForApproval })
        {
            if (approve)
            {
                payout.Status = PayoutStatus.Prepared;
            }
            else
            {
                await CloseRejectedAsync(payout, body.Note?.Trim() is { Length: > 0 } n ? n : "Not approved by finance", ct);
            }
        }
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, approve ? "approval.approved" : "approval.rejected", approval.Summary, "Approval", approval.Id.ToString(), ct: ct);
        await notifier.NotifyUserAsync(approval.RequestedById, new NotificationMessage(NotificationCategory.Money, $"{(approve ? "Approved" : "Not approved")}: {approval.Amount:0} EGP",
            approve ? approval.Summary : $"{approval.Summary} — {body.Note!.Trim()}", "#/admin/payouts", approve ? NotificationTone.Success : NotificationTone.Danger), ct);
        return new ApprovalDto(approval.Id, approval.Kind.ToString(), approval.RefType, payout?.Code, approval.Amount, approval.Summary, approval.Status.ToString(), approval.CreatedAt);
    }

    /// <summary>My movements as Excel: a dashboard sheet (balances) and every row.</summary>
    public async Task<byte[]> StatementAsync(string? walletType, CancellationToken ct)
    {
        var me = Me();
        var type = OwnType(walletType);
        var rows = await payouts.StatementAsync(me, type, ct);
        var wallet = await money.WalletAsync(me, type, ct);
        var dashboard = new ExcelDashboard("Wallet statement", $"{type} · {CairoNow():d MMM yyyy}",
            [new("Available", wallet.AvailableBalance, true), new("Pending", wallet.PendingBalance, true), new("Paid out", wallet.SettledTotal, true), new("Movements", rows.Count)]);
        var sheet = new ExcelSheet("Movements", ["Date", "Kind", "Bucket", "Amount (EGP)", "For", "Available from"],
            rows.Select(r => (IReadOnlyList<object?>)[r.CreatedAt, r.Kind, r.Bucket, r.Amount, $"{r.RefType} {r.RefId}", r.AvailableAt]).ToList());
        return excel.Build(dashboard, sheet);
    }

    private async Task CloseRejectedAsync(PayoutRequest payout, string reason, CancellationToken ct)
    {
        payout.Status = PayoutStatus.Rejected;
        payout.RejectReason = reason;
        Move(payout.Wallet, payout.Amount, LedgerKind.Adjustment, payout);
        await unitOfWork.SaveChangesAsync(ct);
        await notifier.NotifyUserAsync(payout.Wallet.OwnerUserId!.Value, new NotificationMessage(NotificationCategory.Money, "Withdrawal not sent",
            $"{payout.Code}: {reason} · {payout.Amount:0} EGP is back in your balance", "#/dash/wallet", NotificationTone.Warning), ct);
        await audit.LogAsync(AuditSystem.Admin, "payout.rejected", $"Withdrawal {payout.Code} rejected: {reason}", "Payout", payout.Code, ct: ct);
    }

    private LedgerEntry Move(Wallet wallet, decimal amount, LedgerKind kind, PayoutRequest payout)
    {
        var entry = new LedgerEntry { Wallet = wallet, Amount = amount, Bucket = LedgerBucket.Available, Kind = kind, RefType = "Payout",
            RefId = payout.Id, GroupId = Guid.NewGuid(), CreatedAt = clock.UtcNow };
        money.Add(entry);
        wallet.AvailableBalance += amount;
        return entry;
    }

    private async Task<PayoutRequest> StatusAsync(string code, PayoutStatus status, CancellationToken ct)
    {
        var payout = await payouts.FindAsync(code, ct) ?? throw new NotFoundException("Withdrawal not found.");
        Check(payout.Status == status, $"This withdrawal is {payout.Status}, not {status}.", "wrong_status");
        return payout;
    }

    private static WalletType OwnType(string? type) =>
        Enum.TryParse<WalletType>(type ?? nameof(WalletType.PartnerBookings), true, out var t) && t is > WalletType.Customer and < WalletType.PlatformRevenue
            ? t : throw new BadRequestException("Unknown wallet type.", "wallet_type");

    private static void Check(bool ok, string message, string code)
    {
        if (!ok)
        {
            throw new BusinessRuleException(message, code);
        }
    }

    private DateTime CairoNow() => Pitches.CairoTime.Now(clock);

    private Guid Me() => currentUser.UserId ?? throw new UnauthorizedException();

    private static PayoutDto ToDto(PayoutRequest p, WalletType type) => new(p.Code, type.ToString(), p.Amount, p.Method.ToString(), p.Destination,
        p.Status.ToString(), p.TransferRef, p.RejectReason, p.CreatedAt, p.SentAt, null, p.ApprovalId);
}
