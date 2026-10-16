"""Pitches and bookings (ERD B24–B32, B56–B72)."""
from generate import Table as T, E

U = "auth.Users"
NOW = "sql:SYSUTCDATETIME()"
PAYOUT = E("PayoutMethod", "Bank InstaPay")

T("pitch", "Partners", "Partner", "B", "S", doc="A pitch owner's business profile.") \
    .c("UserId", "uniqueidentifier", uq=True, fk=U).c("BusinessName", "nvarchar(120)") \
    .c("BookingCommission", "decimal(5,2)", null=True, check="[BookingCommission] IS NULL OR [BookingCommission] BETWEEN 0 AND 100") \
    .c("StoreCommission", "decimal(5,2)", null=True, check="[StoreCommission] IS NULL OR [StoreCommission] BETWEEN 0 AND 100") \
    .c("PayoutMethod", PAYOUT).c("PayoutDetail", "nvarchar(400)", doc="Encrypted payout account.") \
    .c("Status", E("PartnerStatus", "Active Suspended"), default=0).c("SuspendedReason", "nvarchar(400)", null=True) \
    .c("SlotPostingBlocked", "bit", default=False)

T("pitch", "PartnerStaff", "PartnerStaffMember", "B", doc="A staff account of a pitch owner.") \
    .c("PartnerId", "bigint", fk="pitch.Partners", on="Cascade", collection="Staff").c("UserId", "uniqueidentifier", fk=U) \
    .c("Role", E("PartnerStaffRole", "Manager CheckIn Accountant")).c("IsActive", "bit", default=True) \
    .ix("PartnerId UserId", unique=True)

T("pitch", "Venues", "Venue", "B", "S", doc="A venue (place) owned by a partner.") \
    .c("PartnerId", "bigint", fk="pitch.Partners").c("Name", "nvarchar(120)").c("AreaId", "int", fk="geo.Areas") \
    .c("Address", "nvarchar(300)").c("Lat", "decimal(9,6)", check="[Lat] BETWEEN 22 AND 32") \
    .c("Lng", "decimal(9,6)", check="[Lng] BETWEEN 24 AND 37").c("Phone", "varchar(20)", null=True)

T("pitch", "Pitches", "Pitch", "B", "S V", doc="A playable pitch.") \
    .c("VenueId", "bigint", fk="pitch.Venues").c("Slug", "varchar(80)", uq=True).c("Name", "nvarchar(120)") \
    .c("Format", "tinyint", check="[Format] IN (5, 7, 11)") \
    .c("Surface", E("PitchSurface", "ArtificialTurf NaturalGrass IndoorCourt")).c("IsIndoor", "bit", default=False) \
    .c("PricePerHour", "decimal(12,2)", check="[PricePerHour] > 0") \
    .c("PeakPricePerHour", "decimal(12,2)", null=True, check="[PeakPricePerHour] IS NULL OR [PeakPricePerHour] > 0") \
    .c("PeakFromHour", "tinyint", null=True, check="[PeakFromHour] IS NULL OR [PeakFromHour] <= 23") \
    .c("DescriptionEn", "nvarchar(2000)", null=True).c("DescriptionAr", "nvarchar(2000)", null=True) \
    .c("CancellationPolicyId", "int", fk="pitch.CancellationPolicies").c("AllowCash", "bit", default=True) \
    .c("Status", E("PitchStatus", "WaitingForReview Live Rejected ClosedByOwner ClosedByAdmin"), default=0) \
    .c("ReviewedById", "uniqueidentifier", null=True, fk=U).c("RejectReason", "nvarchar(400)", null=True) \
    .c("Rating", "decimal(3,2)", default=0, check="[Rating] BETWEEN 0 AND 5").c("ReviewsCount", "int", default=0) \
    .ix("Status VenueId").ix("Format PricePerHour")

T("pitch", "PitchPhotos", "PitchPhoto", "B", doc="A photo of a pitch.") \
    .c("PitchId", "bigint", fk="pitch.Pitches", on="Cascade", collection="Photos").c("FileId", "bigint", fk="ops.Files") \
    .c("SortOrder", "int", default=0)

T("pitch", "OpeningHours", "OpeningHour", "P", doc="Opening hours of a pitch on one weekday.") \
    .c("PitchId", "bigint", fk="pitch.Pitches", on="Cascade", collection="OpeningHours") \
    .c("DayOfWeek", "tinyint", check="[DayOfWeek] <= 6").c("OpenHour", "tinyint", check="[OpenHour] <= 23") \
    .c("CloseHour", "tinyint", check="[CloseHour] BETWEEN 1 AND 24").c("IsClosed", "bit", default=False) \
    .ck("Hours", "[CloseHour] > [OpenHour]").ix("PitchId DayOfWeek", unique=True)

T("pitch", "Closures", "Closure", "B", "A", doc="Blocked hours or days of a pitch.") \
    .c("PitchId", "bigint", fk="pitch.Pitches", on="Cascade", collection="Closures") \
    .c("FromDate", "date").c("ToDate", "date").c("FromHour", "tinyint", null=True).c("ToHour", "tinyint", null=True) \
    .c("Reason", "nvarchar(200)") \
    .ck("Dates", "[ToDate] >= [FromDate]") \
    .ck("Hours", "([FromHour] IS NULL AND [ToHour] IS NULL) OR ([FromHour] <= 24 AND [ToHour] <= 24 AND [ToHour] > [FromHour])") \
    .ix("PitchId FromDate")

T("pitch", "PackageOptions", "PackageOption", "B", doc="A package size (1–12 matches) a pitch offers, with its discount.") \
    .c("PitchId", "bigint", fk="pitch.Pitches", on="Cascade", collection="PackageOptions") \
    .c("MatchCount", "tinyint", check="[MatchCount] BETWEEN 1 AND 12") \
    .c("DiscountPercent", "decimal(5,2)", default=0, check="[DiscountPercent] BETWEEN 0 AND 100") \
    .c("Status", E("PackageOptionStatus", "WaitingForApproval Approved Rejected Disabled"), default=0) \
    .c("ApprovedDiscount", "decimal(5,2)", null=True, check="[ApprovedDiscount] IS NULL OR [ApprovedDiscount] BETWEEN 0 AND 100") \
    .c("ReviewedById", "uniqueidentifier", null=True, fk=U).c("ReviewedAt", "datetime2(0)", null=True) \
    .c("RejectReason", "nvarchar(400)", null=True) \
    .ix("PitchId MatchCount", unique=True)

T("pitch", "Offers", "Offer", "B", "A", doc="A quiet-hour discount on a pitch.") \
    .c("PitchId", "bigint", fk="pitch.Pitches", on="Cascade", collection="Offers") \
    .c("DaysMask", "tinyint", check="[DaysMask] BETWEEN 1 AND 127") \
    .c("FromHour", "tinyint", check="[FromHour] <= 24").c("ToHour", "tinyint", check="[ToHour] <= 24") \
    .c("DiscountPercent", "decimal(5,2)", check="[DiscountPercent] > 0 AND [DiscountPercent] <= 90") \
    .c("StartsOn", "date").c("EndsOn", "date").c("IsActive", "bit", default=True) \
    .ck("Hours", "[ToHour] > [FromHour]").ck("Dates", "[EndsOn] >= [StartsOn]")

# ------------------------------------------------------------------------------------------- bookings
T("booking", "Packages", "Package", "B", "V", doc="A prepaid weekly package: 1–12 matches with one QR.") \
    .c("Code", "varchar(12)", uq=True).c("PitchId", "bigint", fk="pitch.Pitches").c("CustomerId", "uniqueidentifier", fk=U) \
    .c("PackageOptionId", "bigint", fk="pitch.PackageOptions").c("PaymentId", "bigint", null=True, fk="money.Payments") \
    .c("MatchCount", "tinyint", check="[MatchCount] BETWEEN 1 AND 12").c("UsedCount", "tinyint", default=0) \
    .c("DayOfWeek", "tinyint", check="[DayOfWeek] <= 6").c("StartHour", "tinyint", check="[StartHour] <= 23") \
    .c("Duration", "tinyint", check="[Duration] BETWEEN 1 AND 4").c("StartsOn", "date").c("EndsOn", "date") \
    .c("DiscountPercent", "decimal(5,2)", check="[DiscountPercent] BETWEEN 0 AND 100") \
    .c("TotalPrice", "decimal(12,2)", check="[TotalPrice] > 0").c("QrSecretHash", "varbinary(64)") \
    .c("ExceptionUsed", "bit", default=False).c("ExceptionById", "uniqueidentifier", null=True, fk=U) \
    .c("Status", E("PackageStatus", "Active Finished Expired Cancelled"), default=0) \
    .ck("Used", "[UsedCount] <= [MatchCount]").ck("Dates", "[EndsOn] >= [StartsOn]")

T("booking", "Series", "Series", "B", doc="A venue membership: a team that plays every week and renews.") \
    .c("PitchId", "bigint", fk="pitch.Pitches").c("TeamName", "nvarchar(80)") \
    .c("ContactUserId", "uniqueidentifier", null=True, fk=U, on="SetNull").c("ContactPhone", "varchar(20)", null=True) \
    .c("DayOfWeek", "tinyint", check="[DayOfWeek] <= 6").c("StartHour", "tinyint", check="[StartHour] <= 23") \
    .c("Duration", "tinyint", check="[Duration] BETWEEN 1 AND 4").c("AutoRenew", "bit", default=True) \
    .c("Status", E("SeriesStatus", "Active Ended"), default=0)

T("booking", "Bookings", "Booking", "B", "V", doc="A booking of one or more consecutive hours.") \
    .c("Code", "varchar(12)", uq=True).c("PitchId", "bigint", fk="pitch.Pitches") \
    .c("CustomerId", "uniqueidentifier", null=True, fk=U) \
    .c("WalkInName", "nvarchar(80)", null=True).c("WalkInPhone", "varchar(20)", null=True) \
    .c("Source", E("BookingSource", "App WalkIn Package Series Tournament FreeSlot")) \
    .c("PackageId", "bigint", null=True, fk="booking.Packages").c("SeriesId", "bigint", null=True, fk="booking.Series") \
    .c("Date", "date").c("StartHour", "tinyint", check="[StartHour] <= 23") \
    .c("Duration", "tinyint", default=1, check="[Duration] BETWEEN 1 AND 4") \
    .c("PitchAmount", "decimal(12,2)", check="[PitchAmount] >= 0") \
    .c("DiscountAmount", "decimal(12,2)", default=0, check="[DiscountAmount] >= 0") \
    .c("PromoCodeId", "bigint", null=True, fk="money.PromoCodes") \
    .c("ServiceFee", "decimal(12,2)", default=0, check="[ServiceFee] >= 0") \
    .c("Total", "decimal(12,2)", check="[Total] >= 0") \
    .c("CommissionRate", "decimal(5,2)", check="[CommissionRate] BETWEEN 0 AND 100") \
    .c("PaymentId", "bigint", null=True, fk="money.Payments").c("PayAtVenue", "bit", default=False) \
    .c("PolicyKey", "varchar(20)").c("WaiverVersionId", "bigint", null=True, fk="content.LegalVersions") \
    .c("Status", E("BookingStatus", "Confirmed CheckedIn Played NoShow CancelledByCustomer CancelledByVenue"), default=0) \
    .c("CheckedInAt", "datetime2(0)", null=True).c("CancelledAt", "datetime2(0)", null=True) \
    .c("RefundAmount", "decimal(12,2)", default=0).c("QrSecretHash", "varbinary(64)", null=True) \
    .ck("Refund", "[RefundAmount] >= 0 AND [RefundAmount] <= [Total]") \
    .ix("PitchId Date").ix("CustomerId Date").ix("Status Date")

T("booking", "BookingSlots", "BookingSlot", "P", doc="One hour of a booking — makes double booking impossible.") \
    .c("BookingId", "bigint", fk="booking.Bookings", on="Cascade", collection="Slots").c("PitchId", "bigint", fk="pitch.Pitches") \
    .c("Date", "date").c("Hour", "tinyint", check="[Hour] <= 23").c("IsActive", "bit", default=True) \
    .ix("PitchId Date Hour", unique=True, filter="[IsActive] = 1", name="UQ_BookingSlots_PitchId_Date_Hour")

T("booking", "SlotHolds", "SlotHold", "P", doc="A 10-minute hold on an hour while the customer pays.") \
    .c("PitchId", "bigint", fk="pitch.Pitches", on="Cascade", collection=False).c("Date", "date") \
    .c("Hour", "tinyint", check="[Hour] <= 23").c("UserId", "uniqueidentifier", fk=U, on="Cascade") \
    .c("ExpiresAt", "datetime2(0)").ix("PitchId Date Hour", unique=True)

T("booking", "QrScans", "QrScan", "P", doc="A QR check-in attempt and its result.") \
    .c("BookingId", "bigint", null=True, fk="booking.Bookings").c("PackageId", "bigint", null=True, fk="booking.Packages") \
    .c("ScannedById", "uniqueidentifier", fk=U).c("PitchId", "bigint", fk="pitch.Pitches") \
    .c("ScannedAt", "datetime2(0)", default=NOW) \
    .c("Result", E("QrScanResult", "Accepted BadSignature OtherVenue WrongDay WrongHour Cancelled AlreadyUsed PackageFinished AccountBlocked")) \
    .c("MatchNumber", "tinyint", null=True).c("Device", "nvarchar(120)", null=True).c("IdempotencyKey", "varchar(64)", uq=True) \
    .ck("Target", "[BookingId] IS NOT NULL OR [PackageId] IS NOT NULL").ix("PackageId ScannedAt")

T("booking", "WaitlistEntries", "WaitlistEntry", "B", doc="A customer waiting for a booked hour.") \
    .c("PitchId", "bigint", fk="pitch.Pitches", on="Cascade", collection=False).c("UserId", "uniqueidentifier", fk=U, on="Cascade") \
    .c("Date", "date").c("Hour", "tinyint", check="[Hour] <= 23") \
    .c("Status", E("WaitlistStatus", "Waiting FreeNow Booked Expired"), default=0).c("NotifiedAt", "datetime2(0)", null=True) \
    .ix("PitchId Date Hour UserId", unique=True)

T("booking", "FreeSlots", "FreeSlot", "B", doc="A free hour posted by a venue as a live card.") \
    .c("PitchId", "bigint", fk="pitch.Pitches").c("PostedById", "uniqueidentifier", fk=U) \
    .c("Date", "date").c("Hour", "tinyint", check="[Hour] <= 23") \
    .c("DiscountPercent", "decimal(5,2)", default=0, check="[DiscountPercent] BETWEEN 0 AND 90") \
    .c("OfferId", "bigint", null=True, fk="pitch.Offers").c("BookingId", "bigint", null=True, fk="booking.Bookings") \
    .c("Status", E("FreeSlotStatus", "Live Booked Expired HiddenByAdmin"), default=0) \
    .ix("PitchId Date Hour", unique=True, filter="[Status] = 0", name="UQ_FreeSlots_LivePerHour")

T("booking", "SlotAlerts", "SlotAlert", "B", doc="A customer's alert for free slots at a pitch or in an area.") \
    .c("UserId", "uniqueidentifier", fk=U, on="Cascade").c("PitchId", "bigint", null=True, fk="pitch.Pitches") \
    .c("AreaId", "int", null=True, fk="geo.Areas") \
    .c("FromHour", "tinyint", null=True, check="[FromHour] IS NULL OR [FromHour] <= 24") \
    .c("ToHour", "tinyint", null=True, check="[ToHour] IS NULL OR [ToHour] <= 24").c("IsActive", "bit", default=True) \
    .ck("Target", "[PitchId] IS NOT NULL OR [AreaId] IS NOT NULL")

T("booking", "Games", "Game", "B", doc="A game looking for players (on one of the host's bookings).") \
    .c("Code", "varchar(12)", uq=True).c("HostId", "uniqueidentifier", fk=U) \
    .c("BookingId", "bigint", uq=True, fk="booking.Bookings").c("Title", "nvarchar(100)", null=True) \
    .c("PlayersNeeded", "tinyint", check="[PlayersNeeded] BETWEEN 1 AND 21").c("Positions", "varchar(100)", null=True) \
    .c("Level", E("GameLevel", "Any Casual Intermediate Competitive")) \
    .c("SharePrice", "decimal(12,2)", default=0, check="[SharePrice] >= 0").c("AutoAccept", "bit", default=False) \
    .c("RequestsClosed", "bit", default=False) \
    .c("Status", E("GameStatus", "Open Full Played Cancelled"), default=0).c("HiddenByAdmin", "bit", default=False)

T("booking", "GameRequests", "GameRequest", "B", doc="A player's request to join a game.") \
    .c("GameId", "bigint", fk="booking.Games", on="Cascade", collection="Requests").c("PlayerId", "uniqueidentifier", fk=U) \
    .c("Position", "varchar(10)", null=True).c("Message", "nvarchar(300)", null=True) \
    .c("PaymentId", "bigint", null=True, fk="money.Payments") \
    .c("Status", E("GameRequestStatus", "Waiting Accepted Declined Withdrawn Removed Waitlisted"), default=0) \
    .ix("GameId PlayerId", unique=True)

T("booking", "PlayerRatings", "PlayerRating", "B", doc="A host's rating of a player after a game.") \
    .c("GameId", "bigint", fk="booking.Games", on="Cascade", collection="Ratings").c("RaterId", "uniqueidentifier", fk=U) \
    .c("PlayerId", "uniqueidentifier", fk=U).c("Stars", "tinyint", null=True, check="[Stars] IS NULL OR [Stars] BETWEEN 1 AND 5") \
    .c("ShowedUp", "bit").ix("GameId PlayerId", unique=True)

T("booking", "Tournaments", "Tournament", "B", doc="A knockout cup or a league at a venue.") \
    .c("PartnerId", "bigint", fk="pitch.Partners").c("PitchId", "bigint", fk="pitch.Pitches").c("Name", "nvarchar(120)") \
    .c("Type", E("TournamentType", "KnockoutCup League")).c("TeamCount", "tinyint") \
    .c("EntryFee", "decimal(12,2)", default=0, check="[EntryFee] >= 0").c("PrizeText", "nvarchar(200)", null=True) \
    .c("StartsOn", "date").c("EndsOn", "date").c("MatchHours", "varchar(50)").c("RulesText", "nvarchar(1000)", null=True) \
    .c("Status", E("TournamentStatus", "Registration Running Finished Cancelled"), default=0) \
    .c("ChampionTeamId", "bigint", null=True, fk="booking.TournamentTeams") \
    .ck("TeamCount", "([Type] = 0 AND [TeamCount] IN (4, 8, 16)) OR ([Type] = 1 AND [TeamCount] BETWEEN 3 AND 10)") \
    .ck("Dates", "[EndsOn] >= [StartsOn]")

T("booking", "TournamentTeams", "TournamentTeam", "B", doc="A team registered in a tournament.") \
    .c("TournamentId", "bigint", fk="booking.Tournaments", on="Cascade", collection="Teams").c("Name", "nvarchar(80)") \
    .c("CaptainId", "uniqueidentifier", fk=U).c("Phone", "varchar(20)") \
    .c("PaymentId", "bigint", null=True, fk="money.Payments").c("WaiverVersionId", "bigint", null=True, fk="content.LegalVersions") \
    .c("Status", E("TournamentTeamStatus", "Registered Withdrawn Refunded"), default=0) \
    .ix("TournamentId Name", unique=True)

T("booking", "TournamentMatches", "TournamentMatch", "B", doc="A match of a tournament.") \
    .c("TournamentId", "bigint", fk="booking.Tournaments", on="Cascade", collection="Matches") \
    .c("Round", "tinyint", check="[Round] >= 1") \
    .c("HomeTeamId", "bigint", null=True, fk="booking.TournamentTeams").c("AwayTeamId", "bigint", null=True, fk="booking.TournamentTeams") \
    .c("BookingId", "bigint", null=True, fk="booking.Bookings") \
    .c("HomeGoals", "tinyint", null=True).c("AwayGoals", "tinyint", null=True) \
    .c("HomePens", "tinyint", null=True).c("AwayPens", "tinyint", null=True) \
    .c("WinnerTeamId", "bigint", null=True, fk="booking.TournamentTeams") \
    .ck("Teams", "[HomeTeamId] IS NULL OR [AwayTeamId] IS NULL OR [HomeTeamId] <> [AwayTeamId]")

T("pitch", "Reviews", "Review", "B", doc="A customer's review of a played booking.") \
    .c("PitchId", "bigint", fk="pitch.Pitches").c("BookingId", "bigint", uq=True, fk="booking.Bookings") \
    .c("UserId", "uniqueidentifier", fk=U).c("Stars", "tinyint", check="[Stars] BETWEEN 1 AND 5") \
    .c("Text", "nvarchar(1000)", null=True).c("Reply", "nvarchar(1000)", null=True) \
    .c("RepliedAt", "datetime2(0)", null=True).c("IsHidden", "bit", default=False).ix("PitchId CreatedAt")

T("pitch", "ReviewReports", "ReviewReport", "B", doc="An owner's report of a review.") \
    .c("ReviewId", "bigint", fk="pitch.Reviews", on="Cascade", collection="Reports").c("ReporterId", "uniqueidentifier", fk=U) \
    .c("Reason", "nvarchar(300)").c("Status", E("ReviewReportStatus", "Open Hidden Kept"), default=0)
