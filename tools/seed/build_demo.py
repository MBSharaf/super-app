"""
Builds the demo seed files (ERD §12 step 11) from the raw front-end export: the same partners, pitches, stores,
products, sellers, designs, orders, tournaments, community posts and AI agents the site shows today, already
normalised to the backend's tables (enum names, natural keys), so the C# seeders only map fields.

Output: src/SuperApp.Infrastructure/Persistence/Seed/Data/Demo/*.json. All people and companies are fictional.
Run from the repository root after export_front_data.py:  python tools/seed/build_demo.py
"""
import json
import math
import pathlib
import re
import sys
from datetime import datetime, timezone

ROOT = pathlib.Path(__file__).resolve().parents[2]
RAW = ROOT / "tools" / "seed" / "_front-data.json"
OUT = ROOT / "src" / "SuperApp.Infrastructure" / "Persistence" / "Seed" / "Data" / "Demo"

ROLE_OF_ACCOUNT = {"owner": "PitchOwner", "seller": "Seller", "printer": "FactoryOwner"}
STAFF = {"acc-admin": ("AdminManager", "hq"), "acc-cfo": ("Finance", "cfo")}   # acc-ceo is the real CEO from .env
# Front-end action codes (js/data/org.js ACTIONS) → backend permission codes (API §7.1)
ACTION_PERMISSIONS = {
    "payout.approve": ["payouts.send"], "payout.prepare": ["payouts.read", "payouts.prepare"], "settle": ["finance.approve"],
    "refund": ["refunds.issue"], "credit": ["credits.issue"], "adjust": ["finance.approve"],
    "approve.partner": ["approvals.partners"], "approve.seller": ["approvals.sellers"], "suspend": ["partners.manage", "sellers.manage"],
    "customer.block": ["customers.manage"], "moderate": ["community.moderate"], "designs": ["designs.review", "designs.reports"],
    "promo": ["codes.manage"], "broadcast": ["broadcasts.send"], "content": ["content.banners", "content.popups", "content.news"],
    "fantasy": ["fantasy.manage"], "factory": ["factories.read", "factories.manage", "shipping.manage"],
    "orders": ["orders.read", "orders.manage", "bookings.read", "bookings.manage"], "settings": ["settings.platform"], "audit": ["audit.read"],
}
# Admin console sections an agent may open (org.js "sections") → the read permissions those screens need
SECTION_PERMISSIONS = {
    "analytics": ["analytics.read"], "revenue": ["revenue.read"], "orders": ["orders.read", "bookings.read"], "factories": ["factories.read"],
    "support": ["support.read"], "customers": ["customers.read"], "partners": ["partners.read"], "sellers": ["sellers.read"],
    "payouts": ["payouts.read"], "audit": ["audit.read"], "returns": ["returns.manage"], "games": ["games.moderate"], "community": ["community.moderate"],
}
ITEM_STATUS = {"received": "Received", "printing": "Printing", "packed": "Packed", "shipped": "Shipped", "delivered": "Delivered", "refused": "Refused"}
DESIGN_STATUS = {"live": "Live", "review": "InReview", "draft": "Draft", "rejected": "Rejected", "paused": "Paused"}
TOURNAMENT_STATUS = {"open": "Registration", "running": "Running", "finished": "Finished", "cancelled": "Cancelled"}


def iso(ms: int) -> str | None:
    return datetime.fromtimestamp(ms / 1000, tz=timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ") if ms else None


def slug(text: str) -> str:
    return re.sub(r"[^a-z0-9]+", "-", text.lower()).strip("-")


class Demo:
    def __init__(self, raw: dict):
        self.data = raw["data"]
        self.db = raw["storage"]["superapp.db.v1"]
        self.people: dict[str, dict] = {}
        self.files: dict[str, dict] = {}

    # ---------- people ----------
    def person(self, key: str, name: str, roles: list[str], email: str | None = None, kind: str = "Person") -> str:
        if key not in self.people:
            base = email or f"{slug(name)}@demo.superapp.example"
            taken = {p["email"] for p in self.people.values()}
            self.people[key] = {"key": key, "name": name, "email": base if base not in taken else base.replace("@", f".{slug(key)}@"),
                                "accountType": kind, "roles": roles}
        return key

    def accounts(self) -> None:
        for a in self.db["accounts"]:
            if a["id"] == "acc-ceo":
                continue
            if a["id"].startswith("acc-ag-"):
                self.person(a["id"], a["name"], ["AgentBase"], a["email"], "AiAgentServiceAccount")
            elif a["id"] in STAFF:
                self.person(a["id"], a["name"], [STAFF[a["id"]][0]], a["email"])
            else:
                self.person(a["id"], a["name"], [ROLE_OF_ACCOUNT.get(a["role"], "Customer")], a["email"])

    def staff_limits(self) -> list:
        roles = {r["id"]: r for r in self.data["org"]["ROLES"]}
        return [{"user": acc, "limit": roles[pos]["limit"] or 0, "secondApprove": pos == "cfo"} for acc, (_, pos) in STAFF.items()]

    # ---------- media ----------
    def file(self, key: str, container: str, content_type: str) -> str:
        self.files.setdefault(key, {"key": key, "container": container, "contentType": content_type})
        return key

    def photo(self, key: str) -> str:
        return self.file(self.data["media"]["photos"].get(key, key), "demo-photos", "image/jpeg")

    # ---------- pitches ----------
    def partners(self) -> list:
        accounts = {a["id"]: a for a in self.db["accounts"]}
        owners = sorted({p["ownerId"] for p in self.db["pitches"]})
        return [{"user": o, "business": accounts[o].get("company") or accounts[o]["name"],
                 "payoutMethod": "InstaPay" if (accounts[o].get("payout") or {}).get("method") == "InstaPay" else "Bank",
                 "payoutDetail": (accounts[o].get("payout") or {}).get("detail", "")} for o in owners]

    def pitches(self) -> list:
        return [{"key": p["id"].removeprefix("p-"), "partner": p["ownerId"], "name": p["name"], "area": p["area"], "address": p["address"],
                 "lat": p["lat"], "lng": p["lng"], "format": int(p["format"].split("-")[0]), "surface": self.surface(p["surface"]),
                 "indoor": p["indoor"], "price": p["price"], "peakPrice": p["peakPrice"] if p["peakPrice"] != p["price"] else None,
                 "about": p["about"], "policy": p.get("cancelPolicy", "standard"), "rating": p["rating"], "reviews": p["reviews"],
                 "open": p["open"][0], "close": min(p["open"][1], 24), "facilities": p["facilities"], "photos": [self.photo(i) for i in p["images"]]}
                for p in self.db["pitches"]]

    @staticmethod
    def surface(text: str) -> str:
        return "NaturalGrass" if "grass" in text.lower() else "IndoorCourt" if "futsal" in text.lower() else "ArtificialTurf"

    # ---------- shop ----------
    def stores(self) -> list:
        out = []
        for s in self.db["stores"]:
            kind = "Official" if s.get("official") else "Seller" if s["ownerId"].startswith("acc-s") else "PitchPartner"
            out.append({"handle": s["slug"], "name": s["name"], "owner": None if kind == "Official" else s["ownerId"], "type": kind,
                        "tagline": s.get("tagline"), "about": s.get("description"), "cover": self.photo(s["cover"]) if s.get("cover") else None})
        for sl in self.db["sellers"]:
            if sl["mode"] == "pod" and not sl.get("storeId"):
                out.append({"handle": sl["handle"], "name": sl["name"], "owner": sl["accountId"], "type": "Seller", "tagline": None,
                            "about": sl.get("bio"), "cover": self.photo(sl["cover"]) if sl.get("cover") else None})
        return out

    def products(self) -> list:
        handles = {s["id"]: s["slug"] for s in self.db["stores"]}
        return [{"slug": p["id"], "store": handles[p["storeId"]], "category": p["cat"], "name": p["name"], "brand": p.get("brand"),
                 "description": p.get("desc"), "price": p["price"], "compareAt": p["was"] if p.get("was", 0) > p["price"] else None,
                 "lowStockAt": p.get("lowStockAt", 5), "rating": p.get("rating", 0), "images": [self.photo(i) for i in p.get("images", [])],
                 "specs": [{"label": k, "value": v} for k, v in p.get("specs", [])], "variants": self.variants(p)} for p in self.db["products"]]

    @staticmethod
    def variants(p: dict) -> list:
        combos = [(s, c) for s in p.get("sizes") or ["One size"] for c in p.get("colors") or [{"name": "Default", "hex": None}]]
        each = math.ceil(p.get("stock", 0) / len(combos))
        return [{"size": s, "color": c["name"], "hex": c.get("hex"), "sku": f"{p['id']}-{slug(s)}-{slug(c['name'])}".upper(), "stock": each} for s, c in combos]

    # ---------- print on demand ----------
    def sellers(self) -> list:
        handles = {s["id"]: s["slug"] for s in self.db["stores"]}
        levels = sorted(self.db["settings"]["sellerLevels"], key=lambda lv: lv["sales"], reverse=True)
        out = []
        for sl in self.db["sellers"]:
            if sl["mode"] == "store":
                continue   # a pitch partner's own shop, already a PitchPartner store
            h = sl.get("history", {})
            level = next(lv["id"] for lv in levels if h.get("sales", 0) >= lv["sales"] and h.get("rating", 0) >= lv["rating"])
            account = next(a for a in self.db["accounts"] if a["id"] == sl["accountId"])
            out.append({"key": sl["id"], "user": sl["accountId"], "store": handles.get(sl.get("storeId"), sl["handle"]), "level": level,
                        "mode": "Both" if sl["mode"] == "both" else "PrintOnDemand", "sales": h.get("sales", 0), "rating": h.get("rating", 0),
                        "payoutMethod": "InstaPay" if (account.get("payout") or {}).get("method") == "InstaPay" else "Bank",
                        "payoutDetail": (account.get("payout") or {}).get("detail", "")})
        return out

    def designs(self) -> list:
        out = []
        for ds in self.db["designs"]:
            front, back = (ds.get("placement") or {}).get("front"), (ds.get("placement") or {}).get("back")
            art = self.file(ds["art"], "demo-art", "image/svg+xml") if ds.get("art") else None
            out.append({"key": ds["id"], "seller": ds["sellerId"], "catalogue": ds["catalogId"], "category": ds.get("category"), "title": ds["title"],
                        "description": ds.get("desc"), "price": ds["price"], "status": DESIGN_STATUS.get(ds["status"], "Draft"),
                        "frontArt": art if front else None, "backArt": art if back else None, "front": front, "back": back,
                        "colors": ds["colors"], "tags": ds.get("tags", []), "views": ds.get("views", 0), "sales": ds.get("sales", 0),
                        "submittedAt": iso(ds.get("submittedAt") or ds.get("createdAt")), "reviewedAt": iso(ds.get("reviewedAt", 0))})
        return out

    # ---------- orders ----------
    def orders(self) -> list:
        sellers = {s["id"]: s for s in self.db["sellers"]}
        stores = {s["id"]: s["slug"] for s in self.db["stores"]}
        titles = {ds["id"]: ds["title"] for ds in self.db["designs"]}   # designs have no key column; titles are unique in the demo
        grouped: dict[str, list] = {}
        for item in self.db["podOrders"]:
            grouped.setdefault(item["orderId"], []).append(item)
        out = []
        for code, items in grouped.items():
            c = items[0]["customer"]
            customer = self.person(c["id"], c["name"], ["Customer"])
            statuses = {i["status"] for i in items}
            subtotal = sum(i["price"] * i["qty"] for i in items)
            fee = 0 if subtotal >= self.data["products"]["shipping"]["freeOver"] else self.data["products"]["shipping"]["standard"]
            out.append({"code": code, "customer": customer, "address": {"name": c["name"], "city": c["city"], "line": c.get("address", "")}, "phone": c.get("phone", ""),
                        "subtotal": subtotal, "deliveryFee": fee, "payment": "CashOnDelivery" if items[0].get("payment") == "cod" else "Online",
                        "status": "Completed" if statuses == {"delivered"} else "Cancelled" if statuses == {"refused"} else "InProgress",
                        "placedAt": iso(items[0]["placedAt"]),
                        "items": [{"design": titles[i["designId"]], "store": stores.get(sellers[i["sellerId"]].get("storeId"), sellers[i["sellerId"]]["handle"]),
                                   "title": i["title"], "size": i["size"], "color": i["color"], "qty": i["qty"], "price": i["price"], "base": i["base"],
                                   "commission": i["base"] - i["partnerCost"], "earning": i["profit"], "status": ITEM_STATUS[i["status"]],
                                   "deliveredAt": iso(i.get("deliveredAt", 0))} for i in items]})
        return out

    # ---------- tournaments, community, agents ----------
    def tournaments(self) -> list:
        out = []
        for t in self.db["tournaments"]:
            dates = sorted(r["date"] for r in t["reserved"])
            hours = sorted({h for r in t["reserved"] for h in r["hours"]})
            teams = [{"name": tm["name"], "captain": self.person(f"cap-{slug(tm['captain'])}", tm["captain"], ["Customer"]), "phone": tm["phone"]} for tm in t["teams"]]
            out.append({"name": t["name"], "partner": t["ownerId"], "pitch": t["pitchId"].removeprefix("p-"), "type": "League" if t["format"] == "league" else "KnockoutCup",
                        "teamCount": t["maxTeams"], "fee": t["fee"], "prize": t.get("prize"), "startsOn": dates[0], "endsOn": dates[-1],
                        "hours": ",".join(map(str, hours)), "rules": t.get("rules"), "status": TOURNAMENT_STATUS[t["status"]], "teams": teams})
        return out

    def posts(self) -> list:
        slugs = {c["id"]: c["slug"] for c in self.db["communities"]}
        out = []
        for p in sorted(self.db["communityPosts"], key=lambda x: x["at"]):
            a = p["author"]
            author = a["id"] if a["id"].startswith("acc-") else self.person(a["id"], a["name"], ["Customer"])
            out.append({"community": slugs[p["communityId"]], "author": author, "asAdmin": a["kind"] == "admin", "text": p["text"],
                        "pinned": p.get("pinned", False), "announcement": p.get("announcement", False), "reactions": p.get("seedReactions", 0),
                        "comments": len(p.get("comments", [])), "at": iso(p["at"])})
        return out

    def agents(self) -> list:
        out = []
        for r in self.data["org"]["ROLES"]:
            if r["holder"] != "ai":
                continue
            permissions = sorted({code for action in r.get("can", []) for code in ACTION_PERMISSIONS[action]}
                                 | {code for section in r.get("sections", []) for code in SECTION_PERMISSIONS.get(section, [])})
            out.append({"position": r["id"], "user": f"acc-ag-{r['id']}", "name": r["name"], "titleEn": r["title"], "duties": r.get("duties", []),
                        "moneyLimit": r.get("limit") or 0, "monthlyBudget": r.get("cost") or 0, "permissions": permissions})
        return out


def write(name: str, value) -> None:
    path = OUT / name
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"  {path.relative_to(ROOT)}  ({len(value) if isinstance(value, list) else len(value.keys())})")


def main() -> None:
    if not RAW.exists():
        sys.exit("Run tools/seed/export_front_data.py first.")
    demo = Demo(json.loads(RAW.read_text(encoding="utf-8")))
    demo.accounts()
    pitches = {"partners": demo.partners(), "pitches": demo.pitches()}
    shop = {"stores": demo.stores(), "products": demo.products()}
    pod = {"sellers": demo.sellers(), "designs": demo.designs()}
    orders, tournaments, posts = demo.orders(), demo.tournaments(), demo.posts()
    write("people.json", {"people": list(demo.people.values()), "staffLimits": demo.staff_limits()})
    write("media.json", list(demo.files.values()))
    write("pitches.json", pitches)
    write("shop.json", shop)
    write("pod.json", pod)
    write("orders.json", orders)
    write("tournaments.json", tournaments)
    write("community.json", posts)
    write("agents.json", demo.agents())


if __name__ == "__main__":
    main()
