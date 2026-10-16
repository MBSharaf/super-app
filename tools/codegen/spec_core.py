"""Part A (lookup / independent tables) and Part B accounts, operations and AI (ERD §3 and §4, B1–B23)."""
from generate import Table as T, E

NOW = "sql:SYSUTCDATETIME()"

# ======================================================================================== Part A
T("geo", "Governorates", "Governorate", "L", doc="One of Egypt's 27 governorates.") \
    .c("Code", "varchar(10)", uq=True).c("NameEn", "nvarchar(80)", uq=True).c("NameAr", "nvarchar(80)", uq=True)

T("auth", "Permissions", "Permission", "L", doc="A fine-grained permission code such as payouts.send.") \
    .c("Code", "varchar(80)", uq=True, check="[Code] LIKE '%.%'") \
    .c("Area", "varchar(40)").c("NameEn", "nvarchar(120)").c("NameAr", "nvarchar(120)") \
    .c("DescriptionEn", "nvarchar(400)", null=True).c("IsSensitive", "bit", default=False) \
    .ix("Area")

T("ops", "Departments", "Department", "L", doc="A company department for the org chart.") \
    .c("Key", "varchar(30)", uq=True).c("NameEn", "nvarchar(80)").c("NameAr", "nvarchar(80)") \
    .c("Color", "char(7)", default="#0B6E4F", check="[Color] LIKE '#%'")

T("ops", "Settings", "Setting", "K", pk=["Key"], doc="A platform setting stored as JSON (key/value).") \
    .c("Key", "varchar(80)").c("Value", "json").c("Group", "varchar(40)") \
    .c("UpdatedAt", "datetime2(0)", default=NOW).c("UpdatedById", "uniqueidentifier", null=True)

T("ops", "FeatureFlags", "FeatureFlag", "B", doc="A CEO kill switch or an API group switch.") \
    .c("Key", "varchar(80)", uq=True) \
    .c("Kind", E("FeatureFlagKind", "KillSwitch ApiGroup")) \
    .c("IsEnabled", "bit", default=True).c("Reason", "nvarchar(400)", null=True) \
    .c("ChangedById", "uniqueidentifier", null=True)

T("ops", "Integrations", "Integration", "I", doc="An external service in the integrations registry.") \
    .c("Key", "varchar(60)", uq=True) \
    .c("Category", E("IntegrationCategory", "Payment Courier Email Sms WhatsApp FootballData AiModel Automation StorageOrEInvoice Geocoding Weather Holidays")) \
    .c("Name", "nvarchar(80)") \
    .c("Mode", E("IntegrationMode", "Simulated Test Live"), default=0) \
    .c("IsEnabled", "bit", default=False).c("IsPrimary", "bit", default=False) \
    .c("ConfigJson", "json", null=True).c("SecretName", "varchar(120)", null=True) \
    .c("SecretSetAt", "datetime2(0)", null=True) \
    .c("Status", E("IntegrationStatus", "Unknown Healthy Degraded Down"), default=0) \
    .c("LastCallAt", "datetime2(0)", null=True).c("LastErrorAt", "datetime2(0)", null=True) \
    .c("LastError", "nvarchar(1000)", null=True).c("DailyQuota", "int", null=True) \
    .ix("Category", unique=True, filter="[IsPrimary] = 1", name="UQ_Integrations_PrimaryPerCategory")

T("ops", "WebhookSubscriptions", "WebhookSubscription", "I", doc="An outgoing webhook (e.g. to n8n).") \
    .c("Name", "nvarchar(80)").c("Url", "nvarchar(500)", check="[Url] LIKE 'https://%' OR [Url] LIKE 'http://localhost%'") \
    .c("Events", "json").c("SecretHash", "varbinary(64)").c("IsEnabled", "bit", default=True) \
    .c("FailureCount", "int", default=0, check="[FailureCount] >= 0")

T("ops", "OutboxEvents", "OutboxEvent", "P", doc="An event saved with the change, delivered to webhooks by a background job.") \
    .c("EventId", "uniqueidentifier", uq=True, default="sql:NEWID()").c("Type", "varchar(80)") \
    .c("PayloadJson", "json").c("OccurredAt", "datetime2(3)", default=NOW) \
    .c("Status", E("OutboxStatus", "Pending Delivered Failed Skipped"), default=0) \
    .c("Attempts", "int", default=0).c("NextAttemptAt", "datetime2(0)", null=True) \
    .ix("Status NextAttemptAt")

T("pitch", "Facilities", "Facility", "L", doc="A pitch facility (lights, parking, showers…).") \
    .c("Key", "varchar(30)", uq=True).c("NameEn", "nvarchar(60)").c("NameAr", "nvarchar(60)").c("Icon", "varchar(30)")

T("pitch", "CancellationPolicies", "CancellationPolicy", "L", doc="Flexible, Standard, Moderate or Strict.") \
    .c("Key", "varchar(20)", uq=True).c("NameEn", "nvarchar(40)").c("NameAr", "nvarchar(40)") \
    .c("FullRefundHours", "smallint", check="[FullRefundHours] >= 0") \
    .c("HalfRefundHours", "smallint", check="[HalfRefundHours] >= 0 AND [HalfRefundHours] <= [FullRefundHours]") \
    .c("SummaryEn", "nvarchar(300)").c("SummaryAr", "nvarchar(300)")

T("shop", "ProductCategories", "ProductCategory", "L", doc="A shop category.") \
    .c("Key", "varchar(40)", uq=True).c("NameEn", "nvarchar(60)").c("NameAr", "nvarchar(60)") \
    .c("Icon", "varchar(30)").c("IsHidden", "bit", default=False)

T("pod", "DesignCategories", "DesignCategory", "L", doc="A design section (football, anime…).") \
    .c("Key", "varchar(40)", uq=True).c("NameEn", "nvarchar(60)", uq=True).c("NameAr", "nvarchar(60)")

T("pod", "SellerLevels", "SellerLevel", "L", doc="Seller level (Starter, Rising, Pro, Elite) and its perks.") \
    .c("Key", "varchar(20)", uq=True).c("NameEn", "nvarchar(40)").c("NameAr", "nvarchar(40)") \
    .c("Rank", "tinyint", uq=True, check="[Rank] <= 9").c("SalesNeeded", "int", check="[SalesNeeded] >= 0") \
    .c("MinRating", "decimal(2,1)", default=0, check="[MinRating] BETWEEN 0 AND 5") \
    .c("ActiveDesigns", "int", null=True, check="[ActiveDesigns] IS NULL OR [ActiveDesigns] > 0") \
    .c("ActiveCodes", "int", null=True, check="[ActiveCodes] IS NULL OR [ActiveCodes] > 0") \
    .c("BaseDiscount", "decimal(12,2)", default=0, check="[BaseDiscount] >= 0") \
    .c("StoreCommission", "decimal(5,2)", check="[StoreCommission] BETWEEN 0 AND 100") \
    .c("ReviewLane", E("ReviewLane", "Normal Priority InstantWithSpotCheck")) \
    .c("SearchBoost", "tinyint", default=0, check="[SearchBoost] <= 5") \
    .c("SampleDiscount", "decimal(5,2)", default=0, check="[SampleDiscount] BETWEEN 0 AND 100") \
    .c("FreeSamplesPerMonth", "tinyint", default=0) \
    .c("FantasyDiscount", "decimal(5,2)", default=0, check="[FantasyDiscount] BETWEEN 0 AND 100") \
    .c("BulkUpload", "bit", default=False).c("TeamKits", "bit", default=False).c("EarlyAccess", "bit", default=False) \
    .c("AnalyticsTier", E("AnalyticsTier", "Basic Advanced AdvancedWithExport")) \
    .c("CalendarTier", "tinyint", check="[CalendarTier] <= 3") \
    .c("SupportTier", E("SupportTier", "Normal Priority AccountManager"))

T("pod", "CatalogProducts", "CatalogProduct", "I", "S", doc="A printable product made by the factory (tee, oversized tee, hoodie).") \
    .c("Key", "varchar(40)", uq=True).c("NameEn", "nvarchar(80)").c("NameAr", "nvarchar(80)") \
    .c("Kind", E("GarmentKind", "Tee Hoodie")) \
    .c("DescriptionEn", "nvarchar(400)").c("DescriptionAr", "nvarchar(400)") \
    .c("BasePrice", "decimal(12,2)", check="[BasePrice] > 0") \
    .c("FactoryCost", "decimal(12,2)", check="[FactoryCost] > 0 AND [FactoryCost] < [BasePrice]") \
    .c("BackPrintExtra", "decimal(12,2)", default=0, check="[BackPrintExtra] >= 0") \
    .c("BigSizeExtra", "decimal(12,2)", default=0, check="[BigSizeExtra] >= 0") \
    .c("MaxPrice", "decimal(12,2)", check="[MaxPrice] > [BasePrice]") \
    .c("HasBackPrint", "bit", default=True).c("Model3dUrl", "nvarchar(300)", null=True) \
    .c("Status", E("CatalogProductStatus", "Active Paused Retired"), default=0)

T("fantasy", "Competitions", "Competition", "L", doc="A fantasy competition (EGY, EPL).") \
    .c("Key", "varchar(10)", uq=True).c("NameEn", "nvarchar(80)").c("NameAr", "nvarchar(80)") \
    .c("ExternalLeagueId", "int", uq=True).c("Country", "nvarchar(40)") \
    .c("SquadBudget", "decimal(6,1)", default=100.0, check="[SquadBudget] > 0") \
    .c("MaxPerClub", "tinyint", default=3, check="[MaxPerClub] BETWEEN 1 AND 5")

T("fantasy", "Clubs", "Club", "B", doc="A football club (from API-FOOTBALL).") \
    .c("ExternalId", "int", uq=True).c("NameEn", "nvarchar(80)").c("NameAr", "nvarchar(80)") \
    .c("ShortName", "varchar(5)").c("KitColor", "char(7)", default="#0D1411").c("KitColor2", "char(7)", default="#FFFFFF") \
    .c("LogoUrl", "nvarchar(300)", null=True)

T("fantasy", "Sponsors", "Sponsor", "B", "S", doc="A fantasy sponsor.") \
    .c("Name", "nvarchar(100)", uq=True).c("Tier", E("SponsorTier", "Gold Silver Bronze")) \
    .c("ContactEmail", "nvarchar(256)", null=True) \
    .c("ContractValue", "decimal(12,2)", default=0, check="[ContractValue] >= 0") \
    .c("StartsOn", "date").c("EndsOn", "date", check="[EndsOn] >= [StartsOn]").c("Color", "char(7)", default="#0B6E4F")

T("content", "LegalDocuments", "LegalDocument", "L", doc="Terms, privacy, refunds, waiver, agreements, cookies.") \
    .c("Key", "varchar(30)", uq=True).c("TitleEn", "nvarchar(120)").c("TitleAr", "nvarchar(120)") \
    .c("RequiresAcceptance", "bit", default=True)

T("content", "HelpTopics", "HelpTopic", "L", doc="A help center topic.") \
    .c("Key", "varchar(30)", uq=True).c("NameEn", "nvarchar(80)").c("NameAr", "nvarchar(80)").c("Icon", "varchar(30)")

T("content", "Banners", "Banner", "B", doc="A home page banner.") \
    .c("Title", "nvarchar(70)", check="LEN([Title]) >= 5").c("Badge", "nvarchar(22)", null=True) \
    .c("CtaText", "nvarchar(22)", null=True).c("Link", "nvarchar(300)", check="[Link] LIKE '#/%'") \
    .c("ImageKey", "nvarchar(300)").c("IsActive", "bit", default=True).c("SortOrder", "int", default=0) \
    .c("ShowFrom", "date", null=True).c("ShowUntil", "date", null=True) \
    .ck("Dates", "[ShowUntil] IS NULL OR [ShowFrom] IS NULL OR [ShowUntil] >= [ShowFrom]")

T("content", "PopupAds", "PopupAd", "B", doc="A pop-up ad with a 3D object.") \
    .c("TitleEn", "nvarchar(60)").c("TitleAr", "nvarchar(60)").c("TextEn", "nvarchar(140)").c("TextAr", "nvarchar(140)") \
    .c("CtaEn", "nvarchar(24)").c("CtaAr", "nvarchar(24)").c("Link", "nvarchar(300)", check="[Link] LIKE '#/%'") \
    .c("Badge", "nvarchar(24)", null=True).c("Color", "char(7)", check="[Color] LIKE '#%'") \
    .c("Shape", E("PopupShape", "Cube Coin Tee3d"), default=0).c("Icons", "varchar(200)", null=True) \
    .c("HideFromSellers", "bit", default=False).c("IsActive", "bit", default=True).c("SortOrder", "int", default=0) \
    .c("ShownCount", "int", default=0, check="[ShownCount] >= 0").c("ClickedCount", "int", default=0, check="[ClickedCount] >= 0")

T("community", "BannedWords", "BannedWord", "B", doc="A word blocked in posts and comments.") \
    .c("Word", "nvarchar(60)", uq=True).c("Language", "char(2)", check="[Language] IN ('ar', 'en')")

T("ops", "PublicHolidays", "PublicHoliday", "I", doc="An Egyptian public holiday (Islamic dates start as estimated).") \
    .c("Country", "char(2)", default="EG").c("Date", "date").c("NameEn", "nvarchar(120)").c("NameAr", "nvarchar(120)") \
    .c("Kind", E("HolidayKind", "National Religious Observance")).c("IsDayOff", "bit", default=True) \
    .c("IsEstimated", "bit", default=False).c("ConfirmedById", "uniqueidentifier", null=True) \
    .c("ConfirmedAt", "datetime2(0)", null=True) \
    .c("Source", "varchar(20)", check="[Source] IN ('nagerdate', 'calendarific', 'admin')") \
    .ix("Country Date NameEn", unique=True).ix("Date")

# ======================================================================================== Part B — accounts
U = "auth.Users"
T("geo", "Areas", "Area", "L", doc="An area inside a governorate.") \
    .c("GovernorateId", "int", fk="geo.Governorates", collection="Areas") \
    .c("NameEn", "nvarchar(80)").c("NameAr", "nvarchar(80)") \
    .c("Lat", "decimal(9,6)", null=True).c("Lng", "decimal(9,6)", null=True) \
    .ix("GovernorateId NameEn", unique=True)

T("auth", "StaffLimits", "StaffLimit", "B", doc="Money limits of an admin, the CFO or an agent's service account.") \
    .c("UserId", "uniqueidentifier", uq=True, fk=U, on="Cascade") \
    .c("RefundLimit", "decimal(12,2)", default=0, check="[RefundLimit] >= 0") \
    .c("CreditLimit", "decimal(12,2)", default=0, check="[CreditLimit] >= 0") \
    .c("PayoutLimit", "decimal(12,2)", default=0, check="[PayoutLimit] >= 0") \
    .c("CanSecondApprove", "bit", default=False)

T("auth", "UserAddresses", "UserAddress", "B", doc="A saved delivery address.") \
    .c("UserId", "uniqueidentifier", fk=U, on="Cascade").c("AreaId", "int", fk="geo.Areas") \
    .c("Label", "nvarchar(40)", default="Home").c("Street", "nvarchar(200)") \
    .c("Building", "nvarchar(20)", null=True).c("Floor", "nvarchar(20)", null=True).c("Apartment", "nvarchar(20)", null=True) \
    .c("Landmark", "nvarchar(120)", null=True).c("Phone", "varchar(20)", check="[Phone] LIKE '+20%'") \
    .c("IsDefault", "bit", default=False) \
    .ix("UserId", unique=True, filter="[IsDefault] = 1", name="UQ_UserAddresses_OneDefault")

T("ops", "Files", "StoredFile", "B", "S", doc="An uploaded or generated file; the bytes live in storage, not in the database.") \
    .c("OwnerUserId", "uniqueidentifier", null=True, fk=U) \
    .c("StorageKey", "nvarchar(400)", uq=True).c("Container", "varchar(40)").c("FileName", "nvarchar(200)") \
    .c("ContentType", "varchar(100)", check="[ContentType] IN ('image/png', 'image/jpeg', 'image/webp', 'image/svg+xml', 'application/pdf', 'model/gltf-binary')") \
    .c("SizeBytes", "bigint", check="[SizeBytes] > 0 AND [SizeBytes] <= 20971520") \
    .c("Width", "int", null=True).c("Height", "int", null=True).c("Sha256", "char(64)").c("IsPrivate", "bit", default=True)

T("auth", "KycDocuments", "KycDocument", "B", doc="A national ID check (number stored encrypted).") \
    .c("UserId", "uniqueidentifier", fk=U).c("NationalId", "varchar(200)", doc="Encrypted 14-digit national ID.") \
    .c("FrontFileId", "bigint", fk="ops.Files").c("BackFileId", "bigint", fk="ops.Files") \
    .c("Status", E("KycStatus", "Pending Verified Rejected"), default=0) \
    .c("ReviewedById", "uniqueidentifier", null=True, fk=U).c("ReviewedAt", "datetime2(0)", null=True) \
    .c("RejectReason", "nvarchar(400)", null=True) \
    .ck("RejectReason", "[Status] <> 2 OR [RejectReason] IS NOT NULL").ix("UserId Status")

T("auth", "Applications", "PartnerApplication", "B", doc="A request to become a pitch owner, a seller or a factory.") \
    .c("Code", "varchar(12)", uq=True).c("ApplicantId", "uniqueidentifier", fk=U) \
    .c("Type", E("ApplicationType", "PitchOwner Seller Factory")).c("AnswersJson", "json") \
    .c("AgreementVersionId", "bigint", fk="content.LegalVersions") \
    .c("Status", E("ApplicationStatus", "Pending Approved Rejected Withdrawn"), default=0) \
    .c("ReviewedById", "uniqueidentifier", null=True, fk=U).c("ReviewedAt", "datetime2(0)", null=True) \
    .c("RejectReason", "nvarchar(400)", null=True) \
    .ck("RejectReason", "[Status] <> 2 OR [RejectReason] IS NOT NULL") \
    .ix("Status Type CreatedAt").ix("ApplicantId Type", unique=True, filter="[Status] = 0", name="UQ_Applications_OneOpenPerType")

T("auth", "ApplicationFiles", "ApplicationFile", "B", doc="A document attached to an application.") \
    .c("ApplicationId", "bigint", fk="auth.Applications", on="Cascade", collection="Files") \
    .c("FileId", "bigint", fk="ops.Files").c("Purpose", E("ApplicationFilePurpose", "IdFront IdBack VenuePhoto Document"))

T("ops", "OrgPositions", "OrgPosition", "B", doc="A box of the company pyramid (human holder or AI agent).") \
    .c("DepartmentId", "int", null=True, fk="ops.Departments").c("ReportsToId", "bigint", null=True, fk="ops.OrgPositions") \
    .c("TitleEn", "nvarchar(120)").c("TitleAr", "nvarchar(120)") \
    .c("HolderUserId", "uniqueidentifier", null=True, fk=U, on="SetNull") \
    .c("DutiesJson", "json", null=True).c("SortOrder", "int", default=0)

T("ai", "Agents", "Agent", "B", "S", doc="An AI agent: a service account with permissions, limits and a model.") \
    .c("ServiceUserId", "uniqueidentifier", uq=True, fk=U).c("PositionId", "bigint", null=True, uq=True, fk="ops.OrgPositions", on="SetNull") \
    .c("Name", "nvarchar(60)", uq=True).c("TitleEn", "nvarchar(120)").c("TitleAr", "nvarchar(120)") \
    .c("Description", "nvarchar(2000)").c("Model", "varchar(60)") \
    .c("Platform", E("AgentPlatform", "N8n DirectApi Mcp"), default=0) \
    .c("MoneyLimit", "decimal(12,2)", default=0, check="[MoneyLimit] >= 0") \
    .c("MonthlyBudget", "decimal(12,2)", default=0, check="[MonthlyBudget] >= 0") \
    .c("Status", E("AgentStatus", "Active Paused EmergencyStopped"), default=1) \
    .c("PausedReason", "nvarchar(400)", null=True)

T("ai", "ApiKeys", "ApiKey", "B", doc="An API key for an agent or n8n (only the hash is stored).") \
    .c("AgentId", "bigint", null=True, fk="ai.Agents", on="Cascade", collection="ApiKeys") \
    .c("Name", "nvarchar(80)").c("Prefix", "char(8)", uq=True).c("KeyHash", "varbinary(64)") \
    .c("Scopes", "json", null=True).c("ExpiresAt", "datetime2(0)", null=True) \
    .c("RevokedAt", "datetime2(0)", null=True).c("LastUsedAt", "datetime2(0)", null=True)

T("ai", "AgentTasks", "AgentTask", "B", doc="A task an agent runs (on an event, a schedule or by hand).") \
    .c("AgentId", "bigint", fk="ai.Agents", on="Cascade", collection="Tasks") \
    .c("Title", "nvarchar(120)").c("Description", "nvarchar(2000)") \
    .c("Trigger", E("AgentTrigger", "Event Schedule Manual")) \
    .c("EventType", "varchar(80)", null=True).c("Cron", "varchar(60)", null=True) \
    .c("ExpectedOutput", "nvarchar(1000)", null=True).c("IsEnabled", "bit", default=True) \
    .c("LastRunAt", "datetime2(0)", null=True) \
    .ck("TriggerDetails", "([Trigger] <> 0 OR [EventType] IS NOT NULL) AND ([Trigger] <> 1 OR [Cron] IS NOT NULL)")

T("ai", "AgentRuns", "AgentRun", "P", doc="One run of an agent with its result and cost.") \
    .c("AgentId", "bigint", fk="ai.Agents").c("TaskId", "bigint", null=True, fk="ai.AgentTasks", on="SetNull") \
    .c("StartedAt", "datetime2(0)", default=NOW).c("FinishedAt", "datetime2(0)", null=True) \
    .c("Result", E("AgentRunResult", "Running Done WaitingForApproval Failed Cancelled"), default=0) \
    .c("Summary", "nvarchar(2000)", null=True) \
    .c("InputTokens", "int", default=0, check="[InputTokens] >= 0").c("OutputTokens", "int", default=0, check="[OutputTokens] >= 0") \
    .c("CostUsd", "decimal(10,4)", default=0, check="[CostUsd] >= 0") \
    .ix("AgentId StartedAt")

T("ops", "AuditLogs", "AuditLog", "P", doc="Append-only company audit log.") \
    .c("At", "datetime2(3)", default=NOW).c("ActorUserId", "uniqueidentifier", null=True, fk=U) \
    .c("ActorAgentId", "bigint", null=True, fk="ai.Agents") \
    .c("System", E("AuditSystem", "Site Partner Seller Factory Admin Ceo ApiOrAgent")) \
    .c("Action", "varchar(80)").c("EntityType", "varchar(60)", null=True).c("EntityId", "varchar(40)", null=True) \
    .c("Summary", "nvarchar(500)").c("DataJson", "json", null=True).c("Ip", "varchar(45)", null=True) \
    .c("CorrelationId", "varchar(64)", null=True) \
    .ix("At").ix("EntityType EntityId").ix("ActorUserId At")

T("ops", "IntegrationUsage", "IntegrationUsage", "P", doc="Calls, errors and quota of one integration on one day.") \
    .c("IntegrationId", "int", fk="ops.Integrations", on="Cascade", collection="Usage").c("Day", "date") \
    .c("Calls", "int", default=0, check="[Calls] >= 0").c("Errors", "int", default=0, check="[Errors] >= 0") \
    .c("QuotaUsed", "int", null=True).c("QuotaLimit", "int", null=True) \
    .ix("IntegrationId Day", unique=True)

T("ops", "WebhookDeliveries", "WebhookDelivery", "P", doc="One delivery attempt of an event to a webhook.") \
    .c("SubscriptionId", "int", fk="ops.WebhookSubscriptions", on="Cascade", collection="Deliveries") \
    .c("OutboxEventId", "bigint", fk="ops.OutboxEvents").c("Attempt", "tinyint", check="[Attempt] >= 1") \
    .c("StatusCode", "smallint", null=True).c("DurationMs", "int", null=True).c("Error", "nvarchar(1000)", null=True) \
    .c("At", "datetime2(0)", default=NOW)

T("ops", "Goals", "Goal", "B", doc="A company goal (OKR).") \
    .c("DepartmentId", "int", null=True, fk="ops.Departments").c("OwnerUserId", "uniqueidentifier", null=True, fk=U, on="SetNull") \
    .c("Title", "nvarchar(200)").c("Quarter", "char(7)", check="[Quarter] LIKE '[0-9][0-9][0-9][0-9]-Q[1-4]'")

T("ops", "KeyResults", "KeyResult", "B", doc="A measurable result of a goal.") \
    .c("GoalId", "bigint", fk="ops.Goals", on="Cascade", collection="KeyResults") \
    .c("Title", "nvarchar(200)").c("Metric", "varchar(60)", null=True) \
    .c("Target", "decimal(14,2)", check="[Target] > 0").c("Current", "decimal(14,2)", default=0)

T("ops", "Budgets", "Budget", "B", doc="A department budget for one month.") \
    .c("DepartmentId", "int", fk="ops.Departments").c("Month", "date") \
    .c("Amount", "decimal(12,2)", check="[Amount] >= 0").ix("DepartmentId Month", unique=True)

T("ops", "ComplianceItems", "ComplianceItem", "B", doc="A legal or tax deadline in the compliance calendar.") \
    .c("DepartmentId", "int", null=True, fk="ops.Departments").c("OwnerUserId", "uniqueidentifier", null=True, fk=U, on="SetNull") \
    .c("Title", "nvarchar(200)").c("DueOn", "date") \
    .c("Repeat", E("ComplianceRepeat", "Once Monthly Quarterly Yearly")).c("DoneAt", "datetime2(0)", null=True)
