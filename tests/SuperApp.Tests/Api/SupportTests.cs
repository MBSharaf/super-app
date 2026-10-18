using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Notifications;
using SuperApp.Domain.Entities.Support;

namespace SuperApp.Tests.Api;

/// <summary>Support tickets and chat, the auto-reply, attachments and the team inbox with assignment, priority and disputes (API §6.4, §7.2).</summary>
[Trait("Category", "Database")]
public sealed class SupportTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
    private readonly PartnerKit kit = new(api);

    /// <summary>A customer with one booking at a live pitch; returns the booking code.</summary>
    private async Task<(ApiClient Customer, Guid UserId, string BookingCode, Guid OwnerId)> BookedCustomerAsync()
    {
        var (_, ownerId, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var (customer, userId) = await kit.PersonAsync(partner: false);
        var booked = await customer.PostAsync("/api/v1/bookings", new { pitchSlug = pitch.GetProperty("slug").GetString(), date = PartnerKit.Day(3), hour = 12, duration = 1, payAtVenue = true, acceptWaiver = true });
        booked.Status.ShouldBe(HttpStatusCode.Created, booked.Body.ToString());
        return (customer, userId, booked.Body.Data().GetProperty("code").GetString()!, ownerId);
    }

    private static object Ticket(string? booking = null, decimal? dispute = null) =>
        new { topic = "Booking", subject = "The pitch lights were off", message = "We could not play the second half.", openedFrom = "Site", bookingCode = booking, orderCode = (string?)null, disputeAmount = dispute };

    [Fact]
    public async Task A_ticket_opens_with_the_auto_reply_and_a_dispute_tells_the_other_side()
    {
        var (customer, userId, code, ownerId) = await BookedCustomerAsync();
        (await customer.PostAsync("/api/v1/support/tickets", new { topic = "Nope", subject = "Hi", message = "", openedFrom = (string?)null, bookingCode = (string?)null, orderCode = (string?)null, disputeAmount = (decimal?)null }))
            .Status.ShouldBe(HttpStatusCode.BadRequest);
        (await customer.PostAsync("/api/v1/support/tickets", Ticket(dispute: 300))).Status.ShouldBe(HttpStatusCode.BadRequest);
        var (stranger, _) = await kit.PersonAsync(partner: false);
        (await stranger.PostAsync("/api/v1/support/tickets", Ticket(code))).Body.Code().ShouldBe("booking");

        var opened = await customer.PostAsync("/api/v1/support/tickets", Ticket(code, 300));
        opened.Status.ShouldBe(HttpStatusCode.Created, opened.Body.ToString());
        var ticket = opened.Body.Data();
        ticket.GetProperty("code").GetString()!.ShouldStartWith("TK-");
        (ticket.GetProperty("status").GetString(), ticket.GetProperty("priority").GetString(), ticket.GetProperty("isDispute").GetBoolean()).ShouldBe(("WaitingForTeam", "High", true));
        var messages = ticket.GetProperty("messages");
        messages.GetArrayLength().ShouldBe(2);
        messages[1].GetProperty("fromTeam").GetBoolean().ShouldBeTrue();
        messages[1].GetProperty("body").GetString()!.ShouldContain("24 hours");
        var email = await api.DbAsync(db => db.Users.Where(u => u.Id == userId).Select(u => u.Email).SingleAsync());
        api.Emails.Sent.ShouldContain(m => m.To == email);
        (await api.DbAsync(db => db.Set<Notification>().AnyAsync(n => n.UserId == ownerId && n.Title.StartsWith("A dispute was opened")))).ShouldBeTrue();
        (await customer.GetAsync("/api/v1/support/tickets/mine")).Body.Data().GetArrayLength().ShouldBe(1);
        (await stranger.GetAsync($"/api/v1/support/tickets/{ticket.GetProperty("code").GetString()}")).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_team_replies_assigns_resolves_and_decides_disputes()
    {
        var (customer, _, booking, _) = await BookedCustomerAsync();
        var code = (await customer.PostAsync("/api/v1/support/tickets", Ticket(booking, 250))).Body.Data().GetProperty("code").GetString()!;
        (await customer.PostAsync($"/api/v1/support/tickets/{code}/messages", new { body = "Any news?" })).Status.ShouldBe(HttpStatusCode.Created);

        using (var form = new MultipartFormDataContent { { new StringContent("Photo of the dark pitch"), "body" }, { new ByteArrayContent(Png), "files", "dark.png" } })
        {
            var attached = await customer.Http.PostAsync($"/api/v1/support/tickets/{code}/attachments", form);
            attached.StatusCode.ShouldBe(HttpStatusCode.Created, await attached.Content.ReadAsStringAsync());
        }

        (await customer.GetAsync("/api/v1/admin/support/tickets")).Status.ShouldBe(HttpStatusCode.Forbidden);
        var ceo = await api.CeoAsync();
        var inbox = (await ceo.GetAsync("/api/v1/admin/support/tickets")).Body.Data();
        inbox.EnumerateArray().Single(t => t.GetProperty("code").GetString() == code).GetProperty("unread").GetInt32().ShouldBe(3);
        (await ceo.GetAsync($"/api/v1/admin/support/tickets/{code}")).Body.Data().GetProperty("unread").GetInt32().ShouldBe(0);
        (await ceo.PostAsync($"/api/v1/admin/support/tickets/{code}/messages", new { body = "Partner says the generator failed.", internalNote = true })).Status.ShouldBe(HttpStatusCode.Created);
        (await ceo.PostAsync($"/api/v1/admin/support/tickets/{code}/messages", new { body = "Sorry about that — we are checking with the venue.", internalNote = false })).Status.ShouldBe(HttpStatusCode.Created);

        var mine = (await customer.GetAsync($"/api/v1/support/tickets/{code}")).Body.Data();
        (mine.GetProperty("status").GetString(), mine.GetProperty("unread").GetInt32()).ShouldBe(("WaitingForCustomer", 1));
        mine.GetProperty("messages").EnumerateArray().ShouldNotContain(m => m.GetProperty("isInternalNote").GetBoolean());
        mine.GetProperty("messages").EnumerateArray().ShouldContain(m => m.GetProperty("attachments").GetArrayLength() == 1);
        (await customer.PostAsync($"/api/v1/support/tickets/{code}/read")).Status.ShouldBe(HttpStatusCode.OK);
        (await customer.GetAsync($"/api/v1/support/tickets/{code}")).Body.Data().GetProperty("unread").GetInt32().ShouldBe(0);

        var ceoId = await api.DbAsync(db => db.Users.Where(u => u.Email == AccountsApiFactory.CeoEmail).Select(u => u.Id).SingleAsync());
        (await ceo.PostAsync($"/api/v1/admin/support/tickets/{code}/assign", new { userId = ceoId })).Body.Data().GetProperty("assignedTo").ValueKind.ShouldBe(JsonValueKind.String);
        var (other, otherId) = await kit.PersonAsync(partner: false);
        (await ceo.PostAsync($"/api/v1/admin/support/tickets/{code}/assign", new { userId = otherId })).Body.Code().ShouldBe("assignee");
        (await ceo.PostAsync($"/api/v1/admin/support/tickets/{code}/priority", new { priority = "Urgent" })).Body.Data().GetProperty("priority").GetString().ShouldBe("Urgent");
        (await ceo.PostAsync($"/api/v1/admin/support/tickets/{code}/dispute", new { refund = 250m, paidBy = "Partner", note = "Venue fault." })).Body.Data().GetProperty("status").GetString().ShouldBe("Resolved");
        (await customer.PostAsync($"/api/v1/support/tickets/{code}/messages", new { body = "Thanks" })).Body.Code().ShouldBe("closed");
        (await api.DbAsync(db => db.Set<TicketMessage>().CountAsync(m => m.Ticket.Code == code && m.IsInternalNote))).ShouldBe(2);

        var plain = (await other.PostAsync("/api/v1/support/tickets", Ticket())).Body.Data().GetProperty("code").GetString();
        (await ceo.PostAsync($"/api/v1/admin/support/tickets/{plain}/dispute", new { refund = 0m, paidBy = "SuperApp", note = "None." })).Body.Code().ShouldBe("not_dispute");
        (await ceo.PostAsync($"/api/v1/admin/support/tickets/{plain}/resolve")).Status.ShouldBe(HttpStatusCode.OK);
        (await other.PostAsync($"/api/v1/support/tickets/{plain}/close")).Body.Data().GetProperty("status").GetString().ShouldBe("Closed");
    }
}
