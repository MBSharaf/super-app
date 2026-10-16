"""Shop, print on demand, factory and shipping (ERD B33–B52, B73–B84)."""
from generate import Table as T, E

U = "auth.Users"
NOW = "sql:SYSUTCDATETIME()"
PAYOUT = E("PayoutMethod", "Bank InstaPay")
SIDE = E("PrintSide", "Front Back")

T("shop", "Stores", "Store", "B", "S", doc="A store: the official Super App Store, a partner's store or a seller's store.") \
    .c("OwnerUserId", "uniqueidentifier", null=True, fk=U) \
    .c("Type", E("StoreType", "Official PitchPartner Seller")) \
    .c("Handle", "varchar(40)", uq=True, check="[Handle] NOT LIKE '%[^a-z0-9-]%'").c("Name", "nvarchar(80)") \
    .c("Tagline", "nvarchar(120)", null=True).c("About", "nvarchar(1000)", null=True) \
    .c("LogoFileId", "bigint", null=True, fk="ops.Files").c("CoverFileId", "bigint", null=True, fk="ops.Files") \
    .c("CoverPositionY", "tinyint", default=50, check="[CoverPositionY] <= 100") \
    .c("AccentColor", "char(7)", default="#17795A") \
    .c("Status", E("StoreStatus", "Active OnBreak Suspended"), default=0) \
    .c("BreakUntil", "date", null=True).c("BreakNote", "nvarchar(200)", null=True) \
    .ix("Type", unique=True, filter="[Type] = 0", name="UQ_Stores_OneOfficialStore")

T("pod", "Sellers", "Seller", "B", "S V", doc="A seller (print on demand and/or own products).") \
    .c("UserId", "uniqueidentifier", uq=True, fk=U).c("StoreId", "bigint", uq=True, fk="shop.Stores") \
    .c("LevelId", "int", fk="pod.SellerLevels").c("Mode", E("SellerMode", "PrintOnDemand OwnProducts Both")) \
    .c("Status", E("SellerStatus", "Active Suspended Frozen"), default=0) \
    .c("FrozenUntil", "datetime2(0)", null=True).c("GraceUntil", "date", null=True) \
    .c("LifetimeSales", "int", default=0, check="[LifetimeSales] >= 0") \
    .c("Rating", "decimal(3,2)", default=0, check="[Rating] BETWEEN 0 AND 5") \
    .c("FeaturedDesignId", "bigint", null=True, fk="pod.Designs").c("FeaturedByAdmin", "bit", default=False) \
    .c("Tags", "nvarchar(300)", null=True).c("PayoutMethod", PAYOUT).c("PayoutDetail", "nvarchar(400)", doc="Encrypted payout account.") \
    .ix("LevelId Status")

T("pod", "SellerStrikes", "SellerStrike", "B", "A", doc="A copyright strike on a seller.") \
    .c("SellerId", "bigint", fk="pod.Sellers").c("DesignId", "bigint", null=True, fk="pod.Designs", on="SetNull") \
    .c("Reason", "nvarchar(400)").c("ExpiresAt", "datetime2(0)") \
    .c("RemovedAt", "datetime2(0)", null=True).c("RemovedReason", "nvarchar(400)", null=True)

T("pod", "LevelReviews", "LevelReview", "B", doc="The monthly level review of a seller.") \
    .c("SellerId", "bigint", fk="pod.Sellers", on="Cascade", collection="LevelReviews").c("Month", "date") \
    .c("FromLevelId", "int", fk="pod.SellerLevels").c("ToLevelId", "int", fk="pod.SellerLevels") \
    .c("Action", E("LevelReviewAction", "Kept Promoted GraceMonth Lowered")).c("Note", "nvarchar(400)", null=True) \
    .ix("SellerId Month", unique=True)

T("pod", "CatalogColors", "CatalogColor", "B", doc="A colour of a printable product.") \
    .c("CatalogProductId", "int", fk="pod.CatalogProducts", on="Cascade", collection="Colors") \
    .c("NameEn", "nvarchar(40)").c("NameAr", "nvarchar(40)").c("Hex", "char(7)", check="[Hex] LIKE '#%'") \
    .c("IsActive", "bit", default=True).c("SortOrder", "int", default=0).ix("CatalogProductId NameEn", unique=True)

T("pod", "CatalogSizes", "CatalogSize", "B", doc="A size of a printable product with its size chart (cm).") \
    .c("CatalogProductId", "int", fk="pod.CatalogProducts", on="Cascade", collection="Sizes") \
    .c("Size", "varchar(5)").c("IsBigSize", "bit", default=False) \
    .c("ChestCm", "decimal(4,1)", check="[ChestCm] > 0").c("LengthCm", "decimal(4,1)", check="[LengthCm] > 0") \
    .c("SleeveCm", "decimal(4,1)", check="[SleeveCm] > 0").c("SortOrder", "int", default=0) \
    .ix("CatalogProductId Size", unique=True)

T("pod", "CatalogDetails", "CatalogDetail", "B", doc="A product detail line (fabric, fit, print, care…).") \
    .c("CatalogProductId", "int", fk="pod.CatalogProducts", on="Cascade", collection="Details") \
    .c("LabelEn", "nvarchar(40)").c("LabelAr", "nvarchar(40)").c("ValueEn", "nvarchar(300)").c("ValueAr", "nvarchar(300)") \
    .c("SortOrder", "int", default=0)

T("pod", "CatalogPrintAreas", "CatalogPrintArea", "B", doc="The print area of one side of a product.") \
    .c("CatalogProductId", "int", fk="pod.CatalogProducts", on="Cascade", collection="PrintAreas").c("Side", SIDE) \
    .c("X", "decimal(5,4)", check="[X] BETWEEN 0 AND 1").c("Y", "decimal(5,4)", check="[Y] BETWEEN 0 AND 1") \
    .c("Width", "decimal(5,4)", check="[Width] BETWEEN 0 AND 1").c("Height", "decimal(5,4)", check="[Height] BETWEEN 0 AND 1") \
    .c("PrintWidthCm", "decimal(4,1)", default=30, check="[PrintWidthCm] > 0").ix("CatalogProductId Side", unique=True)

T("factory", "Factories", "Factory", "B", "S", doc="A print factory.") \
    .c("OwnerUserId", "uniqueidentifier", fk=U).c("Name", "nvarchar(120)", uq=True).c("Address", "nvarchar(300)") \
    .c("AreaId", "int", fk="geo.Areas").c("CapacityPerDay", "int", check="[CapacityPerDay] > 0") \
    .c("SlaHours", "smallint", default=48, check="[SlaHours] BETWEEN 1 AND 240").c("Techniques", "varchar(60)", default="DTF") \
    .c("IntakePaused", "bit", default=False).c("Status", E("FactoryStatus", "Active PausedByAdmin Suspended"), default=0) \
    .c("PayoutDetail", "nvarchar(400)", doc="Encrypted payout account.")

T("factory", "FactoryStaff", "FactoryStaffMember", "B", "S", doc="A factory staff member who signs in with a PIN.") \
    .c("FactoryId", "bigint", fk="factory.Factories").c("Name", "nvarchar(80)") \
    .c("Role", E("FactoryRole", "Owner Supervisor Printer QualityCheck Packer")).c("PinHash", "varbinary(64)") \
    .c("FailedPins", "tinyint", default=0).c("LockedUntil", "datetime2(0)", null=True).c("IsActive", "bit", default=True) \
    .ix("FactoryId Name", unique=True)

T("factory", "FactoryDevices", "FactoryDevice", "B", doc="A registered factory device (tablet, scanner PC).") \
    .c("FactoryId", "bigint", fk="factory.Factories", on="Cascade", collection="Devices").c("Name", "nvarchar(60)") \
    .c("TokenHash", "varbinary(64)", uq=True).c("LastSeenAt", "datetime2(0)", null=True).c("RevokedAt", "datetime2(0)", null=True)

T("factory", "FactoryPrices", "FactoryPrice", "B", doc="What the factory charges per piece for a product.") \
    .c("FactoryId", "bigint", fk="factory.Factories", on="Cascade", collection="Prices") \
    .c("CatalogProductId", "int", fk="pod.CatalogProducts") \
    .c("PricePerPiece", "decimal(12,2)", check="[PricePerPiece] > 0") \
    .c("BackPrintPrice", "decimal(12,2)", default=0, check="[BackPrintPrice] >= 0") \
    .ix("FactoryId CatalogProductId", unique=True)

T("pod", "CatalogChangeRequests", "CatalogChangeRequest", "B", doc="A factory's change to the catalogue, waiting for an admin.") \
    .c("CatalogProductId", "int", fk="pod.CatalogProducts").c("FactoryId", "bigint", fk="factory.Factories") \
    .c("RequestedByStaffId", "bigint", null=True, fk="factory.FactoryStaff") \
    .c("Kind", E("CatalogChangeKind", "Price Colour Size PauseProduct Details")).c("ChangeJson", "json").c("Reason", "nvarchar(400)") \
    .c("Status", E("ChangeRequestStatus", "Pending Approved Rejected"), default=0) \
    .c("ReviewedById", "uniqueidentifier", null=True, fk=U).c("ReviewedAt", "datetime2(0)", null=True) \
    .c("ReviewNote", "nvarchar(400)", null=True).ix("Status")

T("pod", "Designs", "Design", "B", "S V", doc="A seller's design on a printable product (front and/or back artwork).") \
    .c("SellerId", "bigint", fk="pod.Sellers").c("CatalogProductId", "int", fk="pod.CatalogProducts") \
    .c("CategoryId", "int", null=True, fk="pod.DesignCategories").c("Title", "nvarchar(60)", default="") \
    .c("Description", "nvarchar(500)", null=True).c("Price", "decimal(12,2)", check="[Price] >= 0") \
    .c("FrontArtFileId", "bigint", null=True, fk="ops.Files").c("BackArtFileId", "bigint", null=True, fk="ops.Files") \
    .c("FrontX", "decimal(5,4)", null=True).c("FrontY", "decimal(5,4)", null=True).c("FrontW", "decimal(5,4)", null=True) \
    .c("BackX", "decimal(5,4)", null=True).c("BackY", "decimal(5,4)", null=True).c("BackW", "decimal(5,4)", null=True) \
    .c("Status", E("DesignStatus", "Draft InReview Live Rejected Paused Removed"), default=0) \
    .c("RejectReason", "nvarchar(400)", null=True) \
    .c("SubmittedAt", "datetime2(0)", null=True).c("ReviewedAt", "datetime2(0)", null=True) \
    .c("ReviewedById", "uniqueidentifier", null=True, fk=U).c("SpotCheck", "bit", default=False) \
    .c("Views", "int", default=0, check="[Views] >= 0").c("Sales", "int", default=0, check="[Sales] >= 0") \
    .ck("FrontPlacement", "([FrontX] IS NULL AND [FrontY] IS NULL AND [FrontW] IS NULL) OR ([FrontX] BETWEEN 0 AND 1 AND [FrontY] BETWEEN 0 AND 1 AND [FrontW] BETWEEN 0 AND 1)") \
    .ck("BackPlacement", "([BackX] IS NULL AND [BackY] IS NULL AND [BackW] IS NULL) OR ([BackX] BETWEEN 0 AND 1 AND [BackY] BETWEEN 0 AND 1 AND [BackW] BETWEEN 0 AND 1)") \
    .ix("Status CategoryId").ix("SellerId Status").ix("SubmittedAt")

T("pod", "DesignTags", "DesignTag", "K", pk=["DesignId", "Tag"], doc="A search tag of a design.") \
    .c("DesignId", "bigint", fk="pod.Designs", on="Cascade", collection="Tags").c("Tag", "nvarchar(40)").ix("Tag")

T("pod", "DesignReports", "DesignReport", "B", doc="A report of a design (copyright, trademark, offensive).") \
    .c("DesignId", "bigint", fk="pod.Designs").c("ReporterUserId", "uniqueidentifier", null=True, fk=U, on="SetNull") \
    .c("Reason", E("DesignReportReason", "Copyright Trademark Offensive Other")).c("Detail", "nvarchar(600)", null=True) \
    .c("Contact", "nvarchar(120)", null=True).c("Status", E("DesignReportStatus", "Open TakenDown Dismissed"), default=0) \
    .c("HandledById", "uniqueidentifier", null=True, fk=U).c("HandledAt", "datetime2(0)", null=True)

T("shop", "Products", "Product", "B", "S A", doc="A store product.") \
    .c("StoreId", "bigint", fk="shop.Stores").c("CategoryId", "int", fk="shop.ProductCategories") \
    .c("Slug", "varchar(120)", uq=True).c("Name", "nvarchar(150)").c("Brand", "nvarchar(80)", null=True) \
    .c("Description", "nvarchar(2000)", null=True).c("Price", "decimal(12,2)", check="[Price] > 0") \
    .c("CompareAtPrice", "decimal(12,2)", null=True).c("LowStockAt", "int", default=5, check="[LowStockAt] >= 0") \
    .c("Status", E("ProductStatus", "Draft Published Unpublished"), default=0).c("HiddenByAdmin", "bit", default=False) \
    .c("HiddenReason", "nvarchar(400)", null=True).c("Rating", "decimal(3,2)", default=0) \
    .ck("CompareAtPrice", "[CompareAtPrice] IS NULL OR [CompareAtPrice] > [Price]") \
    .ck("HiddenReason", "[HiddenByAdmin] = 0 OR [HiddenReason] IS NOT NULL") \
    .ix("StoreId Status").ix("CategoryId Status Price")

T("shop", "ProductImages", "ProductImage", "B", doc="A product photo.") \
    .c("ProductId", "bigint", fk="shop.Products", on="Cascade", collection="Images").c("FileId", "bigint", fk="ops.Files") \
    .c("SortOrder", "int", default=0)

T("shop", "ProductVariants", "ProductVariant", "B", "V", doc="A size/colour variant with its stock.") \
    .c("ProductId", "bigint", fk="shop.Products", on="Cascade", collection="Variants") \
    .c("Size", "nvarchar(20)", default="One size").c("ColorName", "nvarchar(40)", default="Standard") \
    .c("ColorHex", "char(7)", null=True).c("Sku", "varchar(40)", uq=True) \
    .c("Stock", "int", default=0, check="[Stock] >= 0").c("Reserved", "int", default=0) \
    .c("PriceOverride", "decimal(12,2)", null=True, check="[PriceOverride] IS NULL OR [PriceOverride] > 0") \
    .ck("Reserved", "[Reserved] >= 0 AND [Reserved] <= [Stock]").ix("ProductId Size ColorName", unique=True)

T("shop", "ProductSpecs", "ProductSpec", "P", doc="A specification line of a product.") \
    .c("ProductId", "bigint", fk="shop.Products", on="Cascade", collection="Specs") \
    .c("Label", "nvarchar(60)").c("Value", "nvarchar(200)").c("SortOrder", "int", default=0)

T("shop", "Orders", "Order", "B", "V", doc="A shop order.") \
    .c("Code", "varchar(12)", uq=True).c("CustomerId", "uniqueidentifier", fk=U).c("AddressJson", "json") \
    .c("Phone", "varchar(20)").c("Subtotal", "decimal(12,2)", check="[Subtotal] >= 0") \
    .c("DiscountAmount", "decimal(12,2)", default=0, check="[DiscountAmount] >= 0") \
    .c("DeliveryFee", "decimal(12,2)", default=0, check="[DeliveryFee] >= 0") \
    .c("Total", "decimal(12,2)", check="[Total] >= 0").c("VatAmount", "decimal(12,2)", default=0) \
    .c("PromoCodeId", "bigint", null=True, fk="money.PromoCodes") \
    .c("PaymentMethod", E("OrderPaymentMethod", "Online CashOnDelivery Wallet")) \
    .c("PaymentId", "bigint", null=True, fk="money.Payments") \
    .c("Status", E("OrderStatus", "Placed Paid InProgress Completed Cancelled Refunded"), default=0) \
    .c("DeliverySpeed", E("DeliverySpeed", "Standard Express"), default=0) \
    .ix("CustomerId CreatedAt").ix("Status CreatedAt")

T("shop", "OrderItems", "OrderItem", "B", doc="A line of an order: a store product or a printed design.") \
    .c("OrderId", "bigint", fk="shop.Orders", on="Cascade", collection="Items") \
    .c("Kind", E("OrderItemKind", "StoreProduct PrintedDesign")).c("StoreId", "bigint", fk="shop.Stores") \
    .c("ProductVariantId", "bigint", null=True, fk="shop.ProductVariants").c("DesignId", "bigint", null=True, fk="pod.Designs") \
    .c("Title", "nvarchar(150)").c("Size", "nvarchar(40)").c("Color", "nvarchar(40)") \
    .c("Qty", "int", check="[Qty] BETWEEN 1 AND 20").c("UnitPrice", "decimal(12,2)", check="[UnitPrice] >= 0") \
    .c("BaseAmount", "decimal(12,2)", null=True).c("CommissionAmount", "decimal(12,2)", default=0) \
    .c("SellerEarning", "decimal(12,2)", default=0) \
    .c("Status", E("OrderItemStatus", "Received Printing Packed Shipped Delivered Refused Cancelled Returned"), default=0) \
    .c("DeliveredAt", "datetime2(0)", null=True) \
    .ck("Kind", "([Kind] = 0 AND [ProductVariantId] IS NOT NULL AND [DesignId] IS NULL) OR ([Kind] = 1 AND [DesignId] IS NOT NULL AND [ProductVariantId] IS NULL)") \
    .ix("StoreId Status")

T("shop", "Fulfilments", "Fulfilment", "B", doc="The part of an order one store ships.") \
    .c("OrderId", "bigint", fk="shop.Orders", on="Cascade", collection="Fulfilments").c("StoreId", "bigint", fk="shop.Stores") \
    .c("Status", E("FulfilmentStatus", "New Packed Shipped Delivered Cancelled"), default=0).ix("OrderId StoreId", unique=True)

T("shop", "CartItems", "CartItem", "B", doc="A line in a customer's cart.") \
    .c("UserId", "uniqueidentifier", fk=U, on="Cascade").c("ProductVariantId", "bigint", null=True, fk="shop.ProductVariants") \
    .c("DesignId", "bigint", null=True, fk="pod.Designs").c("Size", "nvarchar(40)", null=True).c("Color", "nvarchar(40)", null=True) \
    .c("Qty", "int", check="[Qty] BETWEEN 1 AND 20") \
    .ck("OneItem", "([ProductVariantId] IS NULL AND [DesignId] IS NOT NULL) OR ([ProductVariantId] IS NOT NULL AND [DesignId] IS NULL)") \
    .ix("UserId ProductVariantId", unique=True, filter="[ProductVariantId] IS NOT NULL", name="UQ_CartItems_Variant") \
    .ix("UserId DesignId Size Color", unique=True, filter="[DesignId] IS NOT NULL", name="UQ_CartItems_Design")

T("factory", "Jobs", "Job", "B", "V", doc="A printed item being made at a factory.") \
    .c("Code", "varchar(16)", uq=True).c("OrderItemId", "bigint", uq=True, fk="shop.OrderItems") \
    .c("FactoryId", "bigint", fk="factory.Factories") \
    .c("Stage", E("JobStage", "New Printing QualityCheck Packing Ready Shipped Cancelled"), default=0) \
    .c("DueAt", "datetime2(0)").c("AssignedStaffId", "bigint", null=True, fk="factory.FactoryStaff") \
    .c("ReprintCount", "tinyint", default=0).c("IsSample", "bit", default=False) \
    .c("DeductionAmount", "decimal(12,2)", default=0, check="[DeductionAmount] >= 0").ix("FactoryId Stage DueAt")

T("factory", "JobEvents", "JobEvent", "P", doc="A stage change of a job.") \
    .c("JobId", "bigint", fk="factory.Jobs", on="Cascade", collection="Events") \
    .c("FromStage", "tinyint", check="[FromStage] <= 6").c("ToStage", "tinyint", check="[ToStage] <= 6") \
    .c("StaffId", "bigint", null=True, fk="factory.FactoryStaff").c("Note", "nvarchar(300)", null=True) \
    .c("At", "datetime2(0)", default=NOW)

T("factory", "JobProblems", "JobProblem", "B", doc="A problem reported on a job.") \
    .c("JobId", "bigint", fk="factory.Jobs", on="Cascade", collection="Problems") \
    .c("ReportedByStaffId", "bigint", null=True, fk="factory.FactoryStaff") \
    .c("Kind", E("JobProblemKind", "OutOfBlanks MachineDown BadArtwork Damaged Other")).c("Detail", "nvarchar(400)") \
    .c("Status", E("JobProblemStatus", "Open Resolved"), default=0) \
    .c("ResolvedById", "uniqueidentifier", null=True, fk=U).c("Resolution", "nvarchar(400)", null=True)

T("pod", "Samples", "Sample", "B", doc="A seller's sample order of their own design.") \
    .c("SellerId", "bigint", fk="pod.Sellers").c("DesignId", "bigint", fk="pod.Designs") \
    .c("Color", "nvarchar(40)").c("Size", "nvarchar(40)").c("Address", "nvarchar(400)") \
    .c("Price", "decimal(12,2)", check="[Price] >= 0").c("IsFree", "bit", default=False) \
    .c("OrderId", "bigint", null=True, fk="shop.Orders") \
    .c("Status", E("SampleStatus", "Requested Approved Rejected Shipped Delivered"), default=0)

T("ship", "Shipments", "Shipment", "B", "V", doc="A courier shipment (or a reverse pickup).") \
    .c("OrderId", "bigint", fk="shop.Orders").c("FulfilmentId", "bigint", null=True, fk="shop.Fulfilments") \
    .c("FactoryId", "bigint", null=True, fk="factory.Factories").c("Provider", "varchar(40)") \
    .c("Mode", E("IntegrationMode", "Simulated Test Live")).c("TrackingNumber", "varchar(60)") \
    .c("CodAmount", "decimal(12,2)", default=0, check="[CodAmount] >= 0").c("LabelFileId", "bigint", null=True, fk="ops.Files") \
    .c("Status", E("ShipmentStatus", "Created AwaitingPickup PickedUp OutForDelivery Delivered Refused Cancelled"), default=0) \
    .c("PickupAt", "datetime2(0)", null=True).c("DeliveredAt", "datetime2(0)", null=True) \
    .c("CodRemittedAt", "datetime2(0)", null=True).c("IsReturn", "bit", default=False) \
    .ix("Provider TrackingNumber", unique=True)

T("ship", "ShipmentEvents", "ShipmentEvent", "P", doc="A courier status update.") \
    .c("ShipmentId", "bigint", fk="ship.Shipments", on="Cascade", collection="Events") \
    .c("Status", "tinyint", check="[Status] <= 6").c("Description", "nvarchar(300)", null=True) \
    .c("RawJson", "json", null=True).c("ProviderEventId", "varchar(80)", null=True, uq=True).c("At", "datetime2(0)")

T("ship", "Returns", "ReturnRequest", "B", doc="A customer's return or exchange request.") \
    .c("Code", "varchar(14)", uq=True).c("OrderId", "bigint", fk="shop.Orders").c("CustomerId", "uniqueidentifier", fk=U) \
    .c("Reason", E("ReturnReason", "WrongSize FaultyOrDamaged WrongItemOrPrint NotAsDescribed ChangedMyMind")) \
    .c("Detail", "nvarchar(600)", null=True) \
    .c("Status", E("ReturnStatus", "Requested Approved Received Refunded Exchanged Reprinting Rejected"), default=0) \
    .c("DecidedById", "uniqueidentifier", null=True, fk=U).c("RejectReason", "nvarchar(400)", null=True) \
    .c("RefundAmount", "decimal(12,2)", default=0, check="[RefundAmount] >= 0").c("CustomerPaysDelivery", "bit", default=False)

T("ship", "ReturnItems", "ReturnItem", "P", doc="An item of a return request.") \
    .c("ReturnId", "bigint", fk="ship.Returns", on="Cascade", collection="Items").c("OrderItemId", "bigint", fk="shop.OrderItems") \
    .c("Qty", "int", check="[Qty] >= 1").c("Action", E("ReturnAction", "Refund Exchange Reprint")) \
    .c("NewSize", "nvarchar(40)", null=True).c("NewColor", "nvarchar(40)", null=True)
