"""Fantasy, communities, support, content, notifications (ERD B95–B124) and junction tables (Part C)."""
from generate import Table as T, E

U = "auth.Users"
NOW = "sql:SYSUTCDATETIME()"

# ------------------------------------------------------------------------------------------- fantasy
T("fantasy", "Seasons", "Season", "B", doc="A season of a competition.") \
    .c("CompetitionId", "int", fk="fantasy.Competitions").c("Year", "smallint", check="[Year] BETWEEN 2020 AND 2100") \
    .c("IsCurrent", "bit", default=False).c("LastSyncedAt", "datetime2(0)", null=True) \
    .ix("CompetitionId Year", unique=True).ix("CompetitionId", unique=True, filter="[IsCurrent] = 1", name="UQ_Seasons_OneCurrent")

T("fantasy", "Players", "Player", "B", doc="A real football player (from API-FOOTBALL).") \
    .c("ExternalId", "int", uq=True).c("ClubId", "bigint", fk="fantasy.Clubs").c("Name", "nvarchar(100)") \
    .c("NameAr", "nvarchar(100)", null=True).c("Position", E("PlayerPosition", "Goalkeeper Defender Midfielder Forward")) \
    .c("Price", "decimal(4,1)", check="[Price] BETWEEN 3.5 AND 15.0").c("PhotoUrl", "nvarchar(300)", null=True) \
    .c("Status", E("PlayerStatus", "Available Doubtful Injured LeftTheClub"), default=0) \
    .c("SelectedByPercent", "decimal(5,2)", default=0).ix("ClubId Position")

T("fantasy", "Gameweeks", "Gameweek", "B", doc="A gameweek of a season.") \
    .c("SeasonId", "bigint", fk="fantasy.Seasons", on="Cascade", collection="Gameweeks") \
    .c("Number", "tinyint", check="[Number] BETWEEN 1 AND 50").c("Deadline", "datetime2(0)") \
    .c("Status", E("GameweekStatus", "Upcoming Live Scoring Finished"), default=0).c("ScoredAt", "datetime2(0)", null=True) \
    .ix("SeasonId Number", unique=True)

T("fantasy", "Fixtures", "Fixture", "B", doc="A real match.") \
    .c("ExternalId", "int", uq=True).c("GameweekId", "bigint", fk="fantasy.Gameweeks", on="Cascade", collection="Fixtures") \
    .c("HomeClubId", "bigint", fk="fantasy.Clubs").c("AwayClubId", "bigint", fk="fantasy.Clubs").c("KickoffAt", "datetime2(0)") \
    .c("Status", E("FixtureStatus", "Scheduled Live Finished Postponed Cancelled"), default=0) \
    .c("HomeGoals", "tinyint", null=True).c("AwayGoals", "tinyint", null=True).c("Minute", "tinyint", null=True) \
    .ck("Clubs", "[HomeClubId] <> [AwayClubId]").ix("KickoffAt")

T("fantasy", "PlayerFixtureStats", "PlayerFixtureStat", "B", doc="A player's statistics in one match.") \
    .c("PlayerId", "bigint", fk="fantasy.Players").c("FixtureId", "bigint", fk="fantasy.Fixtures", on="Cascade", collection="PlayerStats") \
    .c("Minutes", "tinyint", default=0, check="[Minutes] <= 130") \
    .c("Goals", "tinyint", default=0).c("Assists", "tinyint", default=0).c("Saves", "tinyint", default=0) \
    .c("OwnGoals", "tinyint", default=0).c("PenaltiesSaved", "tinyint", default=0).c("PenaltiesMissed", "tinyint", default=0) \
    .c("YellowCards", "tinyint", default=0, check="[YellowCards] <= 2").c("RedCard", "bit", default=False) \
    .c("CleanSheet", "bit", default=False).c("GoalsConceded", "tinyint", default=0) \
    .c("Bonus", "tinyint", default=0, check="[Bonus] <= 3").c("Points", "smallint", default=0) \
    .ix("PlayerId FixtureId", unique=True)

T("fantasy", "FantasyTeams", "FantasyTeam", "B", "V", doc="A customer's fantasy squad for a season.") \
    .c("UserId", "uniqueidentifier", fk=U).c("SeasonId", "bigint", fk="fantasy.Seasons").c("Name", "nvarchar(40)") \
    .c("Bank", "decimal(5,1)", default=0, check="[Bank] >= 0").c("FreeTransfers", "tinyint", default=1, check="[FreeTransfers] <= 5") \
    .c("TotalPoints", "int", default=0).c("OverallRank", "int", null=True).c("ChipsUsed", "varchar(60)", null=True) \
    .ix("UserId SeasonId", unique=True)

T("fantasy", "Picks", "Pick", "P", doc="A player picked in a squad for a gameweek.") \
    .c("TeamId", "bigint", fk="fantasy.FantasyTeams", on="Cascade", collection="Picks") \
    .c("GameweekId", "bigint", fk="fantasy.Gameweeks").c("PlayerId", "bigint", fk="fantasy.Players") \
    .c("Slot", "tinyint", check="[Slot] BETWEEN 1 AND 15").c("IsCaptain", "bit", default=False) \
    .c("IsViceCaptain", "bit", default=False).c("Multiplier", "tinyint", default=1, check="[Multiplier] <= 3") \
    .c("Points", "smallint", default=0) \
    .ix("TeamId GameweekId PlayerId", unique=True).ix("TeamId GameweekId Slot", unique=True)

T("fantasy", "Transfers", "Transfer", "B", doc="A fantasy transfer.") \
    .c("TeamId", "bigint", fk="fantasy.FantasyTeams", on="Cascade", collection="Transfers") \
    .c("GameweekId", "bigint", fk="fantasy.Gameweeks").c("PlayerOutId", "bigint", fk="fantasy.Players") \
    .c("PlayerInId", "bigint", fk="fantasy.Players").c("PriceOut", "decimal(4,1)").c("PriceIn", "decimal(4,1)") \
    .c("PointsHit", "tinyint", default=0).ck("Players", "[PlayerOutId] <> [PlayerInId]")

T("fantasy", "TeamGameweekPoints", "TeamGameweekPoint", "B", doc="A squad's points in a gameweek.") \
    .c("TeamId", "bigint", fk="fantasy.FantasyTeams", on="Cascade", collection="GameweekPoints") \
    .c("GameweekId", "bigint", fk="fantasy.Gameweeks").c("Points", "smallint", default=0) \
    .c("TransfersCost", "tinyint", default=0).c("Rank", "int", null=True).c("ChipPlayed", "varchar(20)", null=True) \
    .ix("TeamId GameweekId", unique=True).ix("GameweekId Points")

T("fantasy", "FantasyGroups", "FantasyGroup", "B", "S", doc="A fantasy league: official, private or paid (paid ones hidden until payments are live).") \
    .c("SeasonId", "bigint", fk="fantasy.Seasons").c("Name", "nvarchar(60)").c("InviteCode", "varchar(10)", uq=True) \
    .c("Type", E("FantasyGroupType", "Official Private Paid")).c("IsHidden", "bit", default=False) \
    .c("MonthlyPrice", "decimal(12,2)", null=True).c("OwnerUserId", "uniqueidentifier", null=True, fk=U) \
    .c("SponsorId", "bigint", null=True, fk="fantasy.Sponsors").c("PrizeText", "nvarchar(200)", null=True) \
    .c("PrizeValue", "decimal(12,2)", default=0) \
    .ck("Price", "([Type] <> 2 AND [MonthlyPrice] IS NULL) OR ([Type] = 2 AND [MonthlyPrice] > 0)")

T("fantasy", "FantasyPasses", "FantasyPass", "B", doc="A Fantasy Pass subscription.") \
    .c("UserId", "uniqueidentifier", fk=U).c("CompetitionId", "int", null=True, fk="fantasy.Competitions") \
    .c("PaymentId", "bigint", null=True, fk="money.Payments").c("Price", "decimal(12,2)", check="[Price] >= 0") \
    .c("StartsAt", "datetime2(0)").c("EndsAt", "datetime2(0)").ck("Dates", "[EndsAt] > [StartsAt]").ix("UserId EndsAt")

# ------------------------------------------------------------------------------------------- communities
T("community", "Communities", "Community", "B", "A", doc="A community (forum) created by Super App.") \
    .c("Slug", "varchar(40)", uq=True).c("Name", "nvarchar(80)").c("About", "nvarchar(500)", null=True) \
    .c("Audience", E("CommunityAudience", "Everyone Sellers ProAndEliteSellers")).c("RulesText", "nvarchar(1000)", null=True) \
    .c("SlowModeSeconds", "int", default=0, check="[SlowModeSeconds] BETWEEN 0 AND 3600") \
    .c("Color", "varchar(30)", null=True).c("Icon", "varchar(30)", null=True).c("IsArchived", "bit", default=False)

T("community", "Posts", "Post", "B", "S", doc="A community post (text, photo, poll).") \
    .c("CommunityId", "bigint", fk="community.Communities").c("AuthorId", "uniqueidentifier", fk=U) \
    .c("AsSuperAppAdmin", "bit", default=False).c("Text", "nvarchar(2000)", check="LEN([Text]) >= 1") \
    .c("ImageFileId", "bigint", null=True, fk="ops.Files").c("HasPoll", "bit", default=False) \
    .c("IsPinned", "bit", default=False).c("IsAnnouncement", "bit", default=False) \
    .c("CommentsClosed", "bit", default=False).c("IsHidden", "bit", default=False) \
    .c("CommentsCount", "int", default=0).c("ReactionsCount", "int", default=0) \
    .ix("CommunityId CreatedAt").ix("AuthorId")

T("community", "PollOptions", "PollOption", "P", doc="An option of a poll.") \
    .c("PostId", "bigint", fk="community.Posts", on="Cascade", collection="PollOptions").c("Text", "nvarchar(80)") \
    .c("SortOrder", "tinyint", default=0, check="[SortOrder] <= 5").c("VotesCount", "int", default=0)

T("community", "Comments", "Comment", "B", "S", doc="A comment or a reply.") \
    .c("PostId", "bigint", fk="community.Posts", on="Cascade", collection="Comments") \
    .c("ParentCommentId", "bigint", null=True, fk="community.Comments").c("AuthorId", "uniqueidentifier", fk=U) \
    .c("AsSuperAppAdmin", "bit", default=False).c("Text", "nvarchar(1000)").c("IsHidden", "bit", default=False)

T("community", "Reactions", "Reaction", "B", doc="A reaction on a post or a comment.") \
    .c("UserId", "uniqueidentifier", fk=U, on="Cascade").c("PostId", "bigint", null=True, fk="community.Posts") \
    .c("CommentId", "bigint", null=True, fk="community.Comments") \
    .c("Type", E("ReactionType", "Like Fire Goal Laugh Clap Sad")) \
    .ck("OneTarget", "([PostId] IS NULL AND [CommentId] IS NOT NULL) OR ([PostId] IS NOT NULL AND [CommentId] IS NULL)") \
    .ix("UserId PostId", unique=True, filter="[PostId] IS NOT NULL", name="UQ_Reactions_Post") \
    .ix("UserId CommentId", unique=True, filter="[CommentId] IS NOT NULL", name="UQ_Reactions_Comment")

T("community", "ContentReports", "ContentReport", "B", doc="A report of a post or a comment.") \
    .c("ReporterId", "uniqueidentifier", fk=U).c("PostId", "bigint", null=True, fk="community.Posts") \
    .c("CommentId", "bigint", null=True, fk="community.Comments") \
    .c("Reason", E("ContentReportReason", "Spam Abuse Scam OffTopic Other")).c("Detail", "nvarchar(400)", null=True) \
    .c("Status", E("ContentReportStatus", "Open Removed Kept RemovedAndMuted"), default=0) \
    .c("HandledById", "uniqueidentifier", null=True, fk=U) \
    .ck("OneTarget", "([PostId] IS NULL AND [CommentId] IS NOT NULL) OR ([PostId] IS NOT NULL AND [CommentId] IS NULL)")

# ------------------------------------------------------------------------------------------- support
T("support", "Tickets", "Ticket", "B", "V", doc="A support ticket with its live chat (and disputes).") \
    .c("Code", "varchar(10)", uq=True).c("OpenedById", "uniqueidentifier", fk=U) \
    .c("OpenedFrom", E("TicketOrigin", "Site PartnerDashboard SellerDashboard Factory")) \
    .c("Topic", E("TicketTopic", "Booking Order Payment Account Fantasy Seller Partner Other")).c("Subject", "nvarchar(150)") \
    .c("BookingId", "bigint", null=True, fk="booking.Bookings", on="SetNull") \
    .c("OrderId", "bigint", null=True, fk="shop.Orders", on="SetNull") \
    .c("AgainstUserId", "uniqueidentifier", null=True, fk=U).c("IsDispute", "bit", default=False) \
    .c("DisputeAmount", "decimal(12,2)", null=True, check="[DisputeAmount] IS NULL OR [DisputeAmount] > 0") \
    .c("Priority", E("TicketPriority", "Low Normal High Urgent"), default=1) \
    .c("Status", E("TicketStatus", "Open WaitingForCustomer WaitingForTeam Resolved Closed"), default=0) \
    .c("AssignedToUserId", "uniqueidentifier", null=True, fk=U).c("AssignedToAgentId", "bigint", null=True, fk="ai.Agents") \
    .c("FirstResponseAt", "datetime2(0)", null=True).c("ResolvedAt", "datetime2(0)", null=True) \
    .ix("Status Priority CreatedAt").ix("OpenedById")

T("support", "TicketMessages", "TicketMessage", "B", doc="A chat message in a ticket.") \
    .c("TicketId", "bigint", fk="support.Tickets", on="Cascade", collection="Messages").c("SenderId", "uniqueidentifier", fk=U) \
    .c("Body", "nvarchar(4000)", check="LEN([Body]) >= 1").c("IsInternalNote", "bit", default=False) \
    .c("ReadAt", "datetime2(0)", null=True).ix("TicketId CreatedAt")

T("support", "TicketAttachments", "TicketAttachment", "P", doc="A file attached to a ticket message.") \
    .c("MessageId", "bigint", fk="support.TicketMessages", on="Cascade", collection="Attachments").c("FileId", "bigint", fk="ops.Files")

# ------------------------------------------------------------------------------------------- content
T("content", "HelpArticles", "HelpArticle", "B", doc="A help center question and answer.") \
    .c("TopicId", "int", fk="content.HelpTopics").c("QuestionEn", "nvarchar(200)").c("QuestionAr", "nvarchar(200)") \
    .c("AnswerEn", "nvarchar(4000)").c("AnswerAr", "nvarchar(4000)").c("SortOrder", "int", default=0) \
    .c("HelpfulCount", "int", default=0).c("NotHelpfulCount", "int", default=0).c("IsPublished", "bit", default=True)

T("content", "HelpVotes", "HelpVote", "B", doc="A 'was this helpful' vote.") \
    .c("ArticleId", "bigint", fk="content.HelpArticles", on="Cascade", collection="Votes") \
    .c("UserId", "uniqueidentifier", null=True, fk=U, on="SetNull").c("VisitorId", "varchar(40)", null=True).c("Helpful", "bit")

T("content", "LegalVersions", "LegalVersion", "B", doc="A version of a legal document (Arabic and English).") \
    .c("DocumentId", "int", fk="content.LegalDocuments", collection="Versions") \
    .c("Version", "smallint", check="[Version] >= 1").c("BodyEn", "nvarchar(max)").c("BodyAr", "nvarchar(max)") \
    .c("SummaryEn", "nvarchar(400)").c("SummaryAr", "nvarchar(400)").c("PublishedAt", "datetime2(0)", null=True) \
    .c("LawyerReviewedAt", "datetime2(0)", null=True).c("LawyerNote", "nvarchar(400)", null=True) \
    .c("ReviewedById", "uniqueidentifier", null=True, fk=U).ix("DocumentId Version", unique=True)

T("content", "LegalAcceptances", "LegalAcceptance", "P", doc="A customer's acceptance of a legal version.") \
    .c("VersionId", "bigint", fk="content.LegalVersions").c("UserId", "uniqueidentifier", fk=U) \
    .c("Context", "varchar(30)").c("ContextId", "bigint", null=True).c("Ip", "varchar(45)", null=True) \
    .c("AcceptedAt", "datetime2(0)", default=NOW).ix("UserId VersionId")

T("content", "CookieConsents", "CookieConsent", "P", doc="A visitor's cookie choice.") \
    .c("VisitorId", "varchar(40)").c("UserId", "uniqueidentifier", null=True, fk=U, on="SetNull") \
    .c("Analytics", "bit").c("PolicyVersion", "smallint").c("At", "datetime2(0)", default=NOW)

T("content", "NewsPosts", "NewsPost", "B", "S", doc="A Super App news story.") \
    .c("AuthorId", "uniqueidentifier", fk=U).c("Category", "varchar(20)") \
    .c("Title", "nvarchar(110)", check="LEN([Title]) >= 10").c("Excerpt", "nvarchar(260)", check="LEN([Excerpt]) >= 20") \
    .c("Body", "nvarchar(max)").c("ImageFileId", "bigint", null=True, fk="ops.Files") \
    .c("IsBreaking", "bit", default=False).c("IsHidden", "bit", default=False).c("PublishedAt", "datetime2(0)", default=NOW)

# ------------------------------------------------------------------------------------------- notifications
CATEGORY = E("NotificationCategory", "Bookings Orders Designs Money Level Store Account Community Support Fantasy System")
T("notify", "Notifications", "Notification", "P", doc="An in-app notification.") \
    .c("UserId", "uniqueidentifier", fk=U, on="Cascade").c("Category", CATEGORY).c("Title", "nvarchar(150)") \
    .c("Body", "nvarchar(500)", null=True).c("Link", "nvarchar(300)", null=True, check="[Link] IS NULL OR [Link] LIKE '#/%'") \
    .c("Tone", E("NotificationTone", "Info Success Warning Danger"), default=0).c("ReadAt", "datetime2(0)", null=True) \
    .c("CreatedAt", "datetime2(0)", default=NOW).ix("UserId ReadAt CreatedAt")

T("notify", "NotificationPreferences", "NotificationPreference", "K", pk=["UserId", "Category"], doc="A user's choice per notification category.") \
    .c("UserId", "uniqueidentifier", fk=U, on="Cascade").c("Category", CATEGORY) \
    .c("InApp", "bit", default=True).c("Email", "bit", default=True).c("Push", "bit", default=False)

T("notify", "EmailOutbox", "OutboxEmail", "P", doc="An email waiting to be sent (or sent).") \
    .c("UserId", "uniqueidentifier", null=True, fk=U, on="SetNull").c("ToAddress", "nvarchar(256)").c("Subject", "nvarchar(200)") \
    .c("Template", "varchar(60)").c("ModelJson", "json").c("Language", "char(2)", default="ar") \
    .c("Status", E("EmailStatus", "Queued Sent Failed Cancelled"), default=0).c("Attempts", "tinyint", default=0) \
    .c("SentAt", "datetime2(0)", null=True).c("Error", "nvarchar(1000)", null=True) \
    .c("CreatedAt", "datetime2(0)", default=NOW).ix("Status CreatedAt")

T("notify", "Broadcasts", "Broadcast", "B", "A", doc="A message sent to a segment of users.") \
    .c("Title", "nvarchar(150)").c("Body", "nvarchar(1000)").c("Link", "nvarchar(300)", null=True) \
    .c("Segment", E("BroadcastSegment", "Everyone Customers Partners Sellers Factories FantasyPlayers")) \
    .c("SendEmail", "bit", default=False).c("ScheduledAt", "datetime2(0)", null=True).c("SentAt", "datetime2(0)", null=True) \
    .c("RecipientsCount", "int", default=0)

# ------------------------------------------------------------------------------------------- Part C — junctions
T("auth", "RolePermissions", "RolePermission", "J", pk=["RoleId", "PermissionId"], doc="Permissions bundled in a role.") \
    .c("RoleId", "uniqueidentifier", fk="auth.Roles", on="Cascade").c("PermissionId", "int", fk="auth.Permissions")

T("auth", "UserPermissions", "UserPermission", "J", pk=["UserId", "PermissionId"], doc="A permission granted directly to a user (e.g. Approvals to one admin).") \
    .c("UserId", "uniqueidentifier", fk=U, on="Cascade").c("PermissionId", "int", fk="auth.Permissions") \
    .c("GrantedById", "uniqueidentifier", fk=U).c("ExpiresAt", "datetime2(0)", null=True)

T("ai", "AgentPermissions", "AgentPermission", "J", pk=["AgentId", "PermissionId"], doc="An agent's permission scope.") \
    .c("AgentId", "bigint", fk="ai.Agents", on="Cascade", collection="Permissions").c("PermissionId", "int", fk="auth.Permissions")

T("pitch", "PitchFacilities", "PitchFacility", "J", pk=["PitchId", "FacilityId"], doc="Facilities of a pitch.") \
    .c("PitchId", "bigint", fk="pitch.Pitches", on="Cascade", collection="Facilities").c("FacilityId", "int", fk="pitch.Facilities")

T("pod", "DesignColors", "DesignColor", "J", pk=["DesignId", "CatalogColorId"], doc="Colours a customer can choose for a design.") \
    .c("DesignId", "bigint", fk="pod.Designs", on="Cascade", collection="Colors").c("CatalogColorId", "bigint", fk="pod.CatalogColors")

T("pod", "SellerFollowers", "SellerFollower", "J", pk=["SellerId", "UserId"], doc="Customers following a store.") \
    .c("SellerId", "bigint", fk="pod.Sellers", on="Cascade", collection="Followers").c("UserId", "uniqueidentifier", fk=U)

T("shop", "WishlistItems", "WishlistItem", "B", doc="A saved product or design.") \
    .c("UserId", "uniqueidentifier", fk=U, on="Cascade").c("ProductId", "bigint", null=True, fk="shop.Products") \
    .c("DesignId", "bigint", null=True, fk="pod.Designs") \
    .ck("OneItem", "([ProductId] IS NULL AND [DesignId] IS NOT NULL) OR ([ProductId] IS NOT NULL AND [DesignId] IS NULL)") \
    .ix("UserId ProductId", unique=True, filter="[ProductId] IS NOT NULL", name="UQ_WishlistItems_Product") \
    .ix("UserId DesignId", unique=True, filter="[DesignId] IS NOT NULL", name="UQ_WishlistItems_Design")

T("money", "PromoCodePitches", "PromoCodePitch", "J", pk=["PromoCodeId", "PitchId"], doc="Pitches a booking code is limited to.") \
    .c("PromoCodeId", "bigint", fk="money.PromoCodes", on="Cascade", collection="Pitches").c("PitchId", "bigint", fk="pitch.Pitches")

T("fantasy", "SeasonClubs", "SeasonClub", "J", pk=["SeasonId", "ClubId"], doc="Clubs playing in a season.") \
    .c("SeasonId", "bigint", fk="fantasy.Seasons", on="Cascade", collection="Clubs").c("ClubId", "bigint", fk="fantasy.Clubs")

T("fantasy", "GroupMembers", "GroupMember", "J", pk=["GroupId", "TeamId"], doc="Squads in a fantasy league.") \
    .c("GroupId", "bigint", fk="fantasy.FantasyGroups", on="Cascade", collection="Members").c("TeamId", "bigint", fk="fantasy.FantasyTeams") \
    .c("Rank", "int", null=True).c("PaidUntil", "datetime2(0)", null=True)

T("community", "CommunityMembers", "CommunityMember", "J", pk=["CommunityId", "UserId"], doc="Members of a community.") \
    .c("CommunityId", "bigint", fk="community.Communities", on="Cascade", collection="Members").c("UserId", "uniqueidentifier", fk=U) \
    .c("Role", E("CommunityRole", "Member Moderator Owner"), default=0) \
    .c("MutedUntil", "datetime2(0)", null=True).c("MuteReason", "nvarchar(300)", null=True)

T("community", "PollVotes", "PollVote", "J", pk=["OptionId", "UserId"], doc="A vote on a poll option (one per poll).") \
    .c("OptionId", "bigint", fk="community.PollOptions", on="Cascade", collection="Votes").c("UserId", "uniqueidentifier", fk=U) \
    .c("PostId", "bigint", fk="community.Posts").ix("PostId UserId", unique=True)
