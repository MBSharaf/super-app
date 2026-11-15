"""
Turns the raw front-end export (tools/seed/_front-data.json, made by export_front_data.py) into the seed files the
backend reads: src/SuperApp.Infrastructure/Persistence/Seed/Data/{Reference,Demo}/*.json.

Only data that comes from the front end is built here; hand-written seed files (governorates, roles, settings,
feature flags, integrations) live next to them and are not touched.

Run from the repository root:  python tools/seed/build_seed.py
"""
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
RAW = ROOT / "tools" / "seed" / "_front-data.json"
OUT = ROOT / "src" / "SuperApp.Infrastructure" / "Persistence" / "Seed" / "Data"

TONES = {"turf": "#0B6E4F", "info": "#1D4ED8", "gold": "#A16207", "blaze": "#E8590C", "ink": "#1F2937", "violet": "#7C3AED"}
EXTRA_AR = {
    "6th of October": "6 أكتوبر",
    "180 g combed cotton, regular fit, DTF print that survives the wash.": "قطن ممشط 180 جرام، قصة عادية، وطباعة DTF بتستحمل الغسيل.",
    "240 g heavyweight cotton, dropped shoulders, boxy streetwear fit.": "قطن تقيل 240 جرام، كتف نازل، وقصة ستريت وير واسعة.",
    "320 g brushed fleece, double-lined hood, kangaroo pocket.": "فليس مبطن 320 جرام، كابيشون بطبقتين، وجيب كانجرو.",
}  # names the site dictionary does not have
GOVERNORATE_OF_CITY = {"Cairo": "C", "Giza": "GZ", "Alexandria": "ALX"}
POLICY_AR = {
    "flexible": "استرداد كامل حتى ساعتين قبل الماتش، وبعدها 50% حتى بداية الماتش.",
    "standard": "استرداد كامل حتى 6 ساعات قبل الماتش، وبعدها 50% حتى بداية الماتش.",
    "moderate": "استرداد كامل حتى 24 ساعة قبل الماتش، و50% حتى 6 ساعات قبله، وبعد كده مفيش استرداد.",
    "strict": "استرداد كامل حتى 48 ساعة قبل الماتش، و50% حتى 24 ساعة قبله، وبعد كده مفيش استرداد.",
}
POLICIES = [
    ("flexible", "Flexible", 2, 0, "Full refund up to 2 hours before kick-off, then 50% until kick-off."),
    ("standard", "Standard", 6, 0, "Full refund up to 6 hours before kick-off, then 50% until kick-off."),
    ("moderate", "Moderate", 24, 6, "Full refund up to 24 hours before, 50% up to 6 hours before, no refund after that."),
    ("strict", "Strict", 48, 24, "Full refund up to 48 hours before, 50% up to 24 hours before, no refund after that."),
]
FACILITY_ICONS = {"lights": "bulb", "parking": "car", "showers": "drop", "lockers": "lock", "cafe": "cup", "water": "drop", "bibs": "shirt",
                  "ac": "snow", "wc": "door", "prayer": "moon", "camera": "camera", "wifi": "wifi", "firstaid": "plus", "seats": "users"}


# Printable area on the flat garment picture, as fractions of the picture (js/components/garment.js AREAS)
PRINT_AREAS = {"tee": {"x": 0.31, "y": 0.2, "w": 0.38, "h": 0.62}, "hoodie": {"x": 0.3, "y": 0.24, "w": 0.4, "h": 0.42}}
MODELS_3D = {"pod-tee": "models/tee.glb", "pod-oversized": "models/tee.glb"}  # the hoodie model is still pending
NO_ACCEPTANCE = {"refunds", "cookies"}  # read-only policies; the others are accepted at sign-up / application
COMPETITIONS = [("egy", "fantasy", 233, "Egypt"), ("epl", "fantasyEPL", 39, "England")]  # API-FOOTBALL league ids


class Builder:
    def __init__(self, raw: dict):
        self.data = raw["data"]
        self.db = raw["storage"]["superapp.db.v1"]
        self.ar_dict = raw["ar"]
        self.missing: set[str] = set()

    def ar(self, en: str) -> str:
        """Arabic from the site's own dictionary; the English text is kept (and reported) when it has none."""
        value = self.ar_dict.get(en) or EXTRA_AR.get(en)
        if value is None:
            self.missing.add(en)
        return value or en

    def areas(self) -> list:
        seen = {}
        for p in self.db["pitches"]:
            seen.setdefault(p["area"], {"governorate": GOVERNORATE_OF_CITY[p["city"]], "en": p["area"], "ar": self.ar(p["area"]), "lat": p["lat"], "lng": p["lng"]})
        order = {"C": 0, "GZ": 1, "ALX": 2}
        return sorted(seen.values(), key=lambda a: (order[a["governorate"]], a["en"]))

    def lookups(self) -> dict:
        levels = self.db["settings"]["sellerLevels"]
        return {
            "facilities": [{"key": k, "en": v, "ar": self.ar(v), "icon": FACILITY_ICONS.get(k, "check")} for k, v in self.data["pitches"]["facilities"].items()],
            "cancellationPolicies": [{"key": k, "en": n, "ar": self.ar(n), "fullHours": f, "halfHours": h, "summaryEn": s, "summaryAr": POLICY_AR[k]} for k, n, f, h, s in POLICIES],
            "productCategories": [{"key": c["id"], "en": c["name"], "ar": self.ar(c["name"]), "icon": c["icon"]} for c in self.data["products"]["categories"]],
            "designCategories": [{"key": c["id"], "en": c["name"], "ar": self.ar(c["name"])} for c in self.db["podCategories"]],
            "sellerLevels": [self.level(i, lv) for i, lv in enumerate(levels)],
        }

    def level(self, rank: int, lv: dict) -> dict:
        return {"key": lv["id"], "en": lv["name"], "ar": self.ar(lv["name"]), "rank": rank + 1, "sales": lv["sales"], "rating": lv["rating"],
                "designs": lv["designs"], "codes": lv["codes"], "baseDiscount": lv["baseDiscount"], "commission": lv["commission"],
                "review": lv["review"], "boost": lv["boost"], "sampleDiscount": lv["sampleDiscount"], "freeSamples": lv["freeSamples"],
                "fantasyDiscount": lv["fantasyDiscount"], "bulk": lv["bulk"], "teamKits": lv["teamKits"], "earlyAccess": lv["earlyAccess"],
                "analytics": lv["analytics"], "calendar": lv["calendar"], "support": lv["support"]}

    def org(self) -> dict:
        org = self.data["org"]
        depts = [{"key": d["key"], "en": d["name"], "ar": self.ar(d["name"]), "color": TONES.get(d["tone"], "#0B6E4F")} for d in org["DEPTS"]]
        positions = [{"key": r["id"], "dept": r["dept"], "reportsTo": r.get("reportsTo"), "en": r["title"], "ar": self.ar(r["title"]),
                      "holder": r["holder"], "name": r.get("name"), "limit": r.get("limit"), "can": r.get("can", []), "duties": r.get("duties", []),
                      "sort": i} for i, r in enumerate(org["ROLES"])]
        return {"departments": depts, "positions": positions, "actions": [{"code": a[0], "en": a[1], "group": a[2]} for a in org["ACTIONS"]]}


    def catalogue(self) -> list:
        out = []
        for c in self.db["podCatalog"]:
            chart = c["sizeChart"]["rows"]
            out.append({
                "key": c["id"], "en": c["name"], "ar": self.ar(c["name"]), "kind": c["kind"].capitalize(),
                "descriptionEn": c["desc"], "descriptionAr": self.ar(c["desc"]),
                "basePrice": c["base"], "factoryCost": c["partnerCost"], "backPrintExtra": c["backExtra"], "bigSizeExtra": c["bigSizeExtra"],
                "maxPrice": c["maxPrice"], "hasBackPrint": "back" in c["areas"], "model3d": MODELS_3D.get(c["id"]),
                "colors": [{"en": x["name"], "ar": self.ar(x["name"]), "hex": x["hex"]} for x in c["colors"]],
                "sizes": [{"size": z, "big": z in c["bigSizes"], "chest": chart[z][0], "length": chart[z][1], "sleeve": chart[z][2]} for z in c["sizes"]],
                "details": [{"labelEn": k, "labelAr": self.ar(k), "valueEn": v, "valueAr": self.ar(v)} for k, v in c["details"]],
                "printAreas": [{"side": side.capitalize(), **PRINT_AREAS[c["kind"]]} for side in c["areas"]],
            })
        return out

    def content(self) -> dict:
        legal = [{"key": doc["key"], "titleEn": doc["title"]["en"], "titleAr": doc["title"]["ar"], "acceptance": doc["key"] not in NO_ACCEPTANCE,
                  "summaryEn": doc["summary"]["en"], "summaryAr": doc["summary"]["ar"],
                  "bodyEn": self.markdown(doc["sections"], "en"), "bodyAr": self.markdown(doc["sections"], "ar")}
                 for doc in self.data["legal"]["DOCS"]]
        help_data = self.data["help"]
        topics = [{"key": k, "icon": i, "en": en, "ar": ar} for k, i, en, ar in help_data["TOPICS"]]
        articles = [{"topic": q["topic"], "qEn": q["q"]["en"], "qAr": q["q"]["ar"], "aEn": q["a"]["en"], "aAr": q["a"]["ar"]} for q in help_data["QA"]]
        banners = [{"title": b["title"], "badge": b["badge"] or None, "cta": b["cta"] or None, "link": b["link"], "image": b["image"],
                    "active": b["active"], "order": b["order"]} for b in self.db["banners"]]
        popups = [self.popup(i, p) for i, p in enumerate(self.data["promoPops"])]
        communities = [{"slug": c["slug"], "name": c["name"], "about": c["about"], "audience": c["audience"], "rules": c.get("rules", []),
                        "slowMode": c.get("slowMode", 0), "color": c.get("color"), "icon": c.get("icon"), "order": c.get("order", 0)}
                       for c in self.db["communities"]]
        words = self.db["settings"]["community"]["bannedWords"]
        return {"legal": legal, "helpTopics": topics, "helpArticles": articles, "banners": banners, "popups": popups,
                "communities": communities, "bannedWords": [{"word": w, "language": "ar" if any("؀" <= ch <= "ۿ" for ch in w) else "en"} for w in words]}

    @staticmethod
    def markdown(sections: list, lang: str) -> str:
        return "\n\n".join(f"## {s['h'][lang]}\n\n" + "\n\n".join(p[lang] for p in s["p"]) for s in sections)

    def popup(self, i: int, p: dict) -> dict:
        shape = {"cube": "Cube", "coin": "Coin", "tee": "Tee3d"}[p["shape"]]
        return {"titleEn": p["title"], "titleAr": self.ar(p["title"]), "textEn": p["text"], "textAr": self.ar(p["text"]),
                "ctaEn": p["cta"], "ctaAr": self.ar(p["cta"]), "link": p["link"], "badge": p.get("badge"), "color": p["color"], "shape": shape,
                "icons": ",".join(p["icons"]) if p.get("icons") else None, "hideFromSellers": bool(p.get("forNonSellers")), "active": p["active"], "order": i + 1}

    def fantasy(self) -> list:
        out = []
        for key, source, league, country in COMPETITIONS:
            f = self.data[source]
            out.append({"key": key, "en": f["label"], "ar": self.ar(f["label"]), "externalLeagueId": league, "country": country,
                        "budget": f["rules"]["budget"], "maxPerClub": f["rules"]["maxPerTeam"]})
        return out


def write(path: pathlib.Path, value) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"  {path.relative_to(ROOT)}")


def main() -> None:
    if not RAW.exists():
        sys.exit("Run tools/seed/export_front_data.py first.")
    b = Builder(json.loads(RAW.read_text(encoding="utf-8")))
    write(OUT / "Reference" / "areas.json", b.areas())
    write(OUT / "Reference" / "lookups.json", b.lookups())
    write(OUT / "Reference" / "org.json", b.org())
    write(OUT / "Reference" / "catalogue.json", b.catalogue())
    write(OUT / "Reference" / "content.json", b.content())
    write(OUT / "Reference" / "fantasy.json", b.fantasy())
    if b.missing:
        print("No Arabic in the site dictionary (English kept):", "; ".join(sorted(b.missing)))


if __name__ == "__main__":
    main()
