"""
One-time scaffolding of the Domain entities, enums and EF Core Fluent API configurations from the ERD.

    python tools/codegen/generate.py

The table definitions live in tools/codegen/spec_*.py and follow docs/backend/ERD.md exactly (columns, types,
NOT NULL, UNIQUE, DEFAULT, CHECK, FOREIGN KEY + ON DELETE, indexes). The generated C# files are the source of truth
afterwards: if a file was edited by hand, change it by hand — do not re-run the generator over it.

Hand-written (not generated): Domain/Common/*, auth Users/Roles (ASP.NET Identity), the AppDbContext and the
shared conventions (CreatedAt/UpdatedAt, soft delete, row version, author).
"""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
DOMAIN = ROOT / "src" / "SuperApp.Domain"
INFRA = ROOT / "src" / "SuperApp.Infrastructure"

FOLDERS = {  # schema -> C# folder / namespace part
    "auth": "Auth", "geo": "Geo", "ops": "Ops", "ai": "Ai", "pitch": "Pitches", "booking": "Bookings", "shop": "Shop",
    "pod": "Pod", "factory": "Factories", "ship": "Shipping", "money": "Money", "fantasy": "Fantasy",
    "community": "Communities", "support": "Support", "content": "Content", "notify": "Notifications",
}
TABLES: dict[str, "Table"] = {}   # "schema.Table" -> Table
ENUMS: dict[str, "Enum"] = {}
EXTERNAL = {"auth.Users": ("User", "Auth"), "auth.Roles": ("Role", "Auth")}  # hand-written Identity classes


# ----------------------------------------------------------------------------------------------- model
class Enum:
    def __init__(self, name, members, folder, doc=""):
        self.name, self.members, self.folder, self.doc = name, members, folder, doc


def E(name, members, doc=""):
    """Enum stored as tinyint 0..n-1. `members` is a space-separated list of PascalCase names."""
    return ("enum", name, members.split(), doc)


class Col:
    def __init__(self, name, type_, null=False, default=None, uq=False, fk=None, on="NoAction", nav=None,
                 check=None, doc="", uq_filter=None, collection=None):
        self.name, self.type, self.null, self.default, self.uq = name, type_, null, default, uq
        self.fk, self.on, self.nav, self.check, self.doc, self.uq_filter = fk, on, nav, check, doc, uq_filter
        self.collection = collection  # name of the collection on the principal (cascade children), or False


class Table:
    def __init__(self, schema, name, cls, base, mods="", doc="", pk=None):
        self.schema, self.name, self.cls, self.base, self.mods, self.doc = schema, name, cls, base, mods.split(), doc
        self.pk = pk
        self.cols: list[Col] = []
        self.checks: list[tuple[str, str]] = []
        self.indexes: list[tuple] = []
        self.folder = FOLDERS[schema]
        TABLES[f"{schema}.{name}"] = self

    def c(self, *a, **k):
        self.cols.append(Col(*a, **k))
        return self

    def ck(self, suffix, sql):
        self.checks.append((suffix, sql))
        return self

    def ix(self, cols, unique=False, filter=None, name=None):
        self.indexes.append((tuple(cols.split()) if isinstance(cols, str) else tuple(cols), unique, filter, name))
        return self


# ----------------------------------------------------------------------------------------------- types
def cs_type(col: Col, table: Table) -> tuple[str, str]:
    """Returns (C# type, kind) where kind drives the initializer and config."""
    t = col.type
    if isinstance(t, tuple) and t[0] == "enum":
        _, name, members, doc = t
        if name not in ENUMS:
            ENUMS[name] = Enum(name, members, table.folder, doc)
        return name, "enum"
    base = re.match(r"[a-z0-9]+", t).group(0)
    return {
        "bigint": ("long", "num"), "int": ("int", "num"), "smallint": ("short", "num"), "tinyint": ("byte", "num"),
        "bit": ("bool", "bool"), "decimal": ("decimal", "dec"), "date": ("DateOnly", "date"),
        "datetime2": ("DateTime", "dt"), "datetimeoffset": ("DateTimeOffset", "dt"), "uniqueidentifier": ("Guid", "guid"),
        "varbinary": ("byte[]", "bytes"), "rowversion": ("byte[]", "bytes"), "nvarchar": ("string", "str"),
        "varchar": ("string", "str"), "char": ("string", "str"), "json": ("string", "str"),
    }[base]


def cs_literal(value, kind, cstype):
    if kind == "enum":
        return f"{cstype}.{ENUMS[cstype].members[value]}"
    if kind == "bool":
        return "true" if value else "false"
    if kind == "dec":
        return f"{value}m"
    if kind == "str":
        return '"' + str(value) + '"'
    if kind == "num":
        return f"({cstype}){value}" if cstype in ("byte", "short") else str(value)
    raise ValueError(f"no literal for {kind}")


# ----------------------------------------------------------------------------------------------- helpers
def target(fk):
    if fk in EXTERNAL:
        return EXTERNAL[fk]
    t = TABLES[fk]
    return t.cls, t.folder


def nav_name(col):
    if col.nav:
        return col.nav
    return col.name[:-2] if col.name.endswith("Id") else col.name + "Ref"


def bases(t: Table):
    b = {"B": "BaseEntity", "I": "IntBaseEntity", "L": "LookupEntity", "P": "Entity<long>", "J": None, "K": None}[t.base]
    out = [b] if b else []
    out += [{"S": "ISoftDelete", "V": "IHasRowVersion", "A": "IHasAuthor"}[m] for m in t.mods]
    return out


def write(path: pathlib.Path, text: str):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text.replace("\r\n", "\n"), encoding="utf-8")


# ----------------------------------------------------------------------------------------------- entity
def emit_entity(t: Table, children: dict):
    usings, props, navs = {"SuperApp.Domain.Common"}, [], []
    for col in t.cols:
        cst, kind = cs_type(col, t)
        if kind == "enum":
            usings.add("SuperApp.Domain.Enums")
        nullable = "?" if col.null else ""
        init = ""
        if col.default is not None and not col.null:
            d = col.default
            if isinstance(d, str) and d.startswith("sql:"):
                init = {"NEWID()": " = Guid.NewGuid();", "SYSUTCDATETIME()": " = DateTime.UtcNow;"}.get(d[4:], "")
            elif not (d is False or d == 0 and kind in ("num", "dec", "enum")):
                init = f" = {cs_literal(d, kind, cst)};"  # CLR defaults (false, 0, first enum member) need no initializer
        elif not col.null and kind == "str":
            init = " = string.Empty;"
        elif not col.null and kind == "bytes":
            init = " = [];"
        doc = f"    /// <summary>{col.doc}</summary>\n" if col.doc else ""
        props.append(f"{doc}    public {cst}{nullable} {col.name} {{ get; set; }}{init}")
        if col.fk:
            tcls, tfolder = target(col.fk)
            if tfolder != t.folder:
                usings.add(f"SuperApp.Domain.Entities.{tfolder}")
            if col.null:
                navs.append(f"    public {tcls}? {nav_name(col)} {{ get; set; }}")
            else:
                navs.append(f"    public {tcls} {nav_name(col)} {{ get; set; }} = null!;")
    if "S" in t.mods:
        props += ["    public bool IsDeleted { get; set; }", "    public DateTime? DeletedAt { get; set; }"]
    if "V" in t.mods:
        props.append("    public byte[] RowVersion { get; set; } = [];")
    if "A" in t.mods:
        props.append("    public Guid? CreatedById { get; set; }")
    if t.base == "J":
        props.append("    public DateTime CreatedAt { get; set; }")
    for (child, colname, cname) in children.get(f"{t.schema}.{t.name}", []):
        if child.folder != t.folder:
            usings.add(f"SuperApp.Domain.Entities.{child.folder}")
        navs.append(f"    public ICollection<{child.cls}> {cname} {{ get; set; }} = [];")
    usings.discard(f"SuperApp.Domain.Entities.{t.folder}")
    head = "\n".join(f"using {u};" for u in sorted(usings))
    inh = bases(t)
    inherit = f" : {', '.join(inh)}" if inh else ""
    body = "\n".join(props)
    if navs:
        body += "\n\n    // Navigation\n" + "\n".join(navs)
    text = (f"{head}\n\nnamespace SuperApp.Domain.Entities.{t.folder};\n\n"
            f"/// <summary>{t.doc} Table <c>{t.schema}.{t.name}</c>.</summary>\n"
            f"public class {t.cls}{inherit}\n{{\n{body}\n}}\n")
    write(DOMAIN / "Entities" / t.folder / f"{t.cls}.cs", text)


# ----------------------------------------------------------------------------------------------- configuration
def emit_config(t: Table, children_of: dict):
    lines, checks, usings = [], list(t.checks), {"Microsoft.EntityFrameworkCore", "Microsoft.EntityFrameworkCore.Metadata.Builders",
                                                 f"SuperApp.Domain.Entities.{t.folder}"}
    soft = "S" in t.mods
    if t.pk:
        keys = ", ".join(f"x.{k}" for k in t.pk)
        lines.append(f"        builder.HasKey(x => new {{ {keys} }});" if len(t.pk) > 1 else f"        builder.HasKey(x => x.{t.pk[0]});")
    else:
        lines.append("        builder.HasKey(x => x.Id);")
    for col in t.cols:
        cst, kind = cs_type(col, t)
        p = f"        builder.Property(x => x.{col.name})"
        ty = col.type if isinstance(col.type, str) else "tinyint"
        if kind == "enum":
            usings.add("SuperApp.Domain.Enums")
            n = len(ENUMS[cst].members)
            checks.append((col.name, f"[{col.name}] BETWEEN 0 AND {n - 1}" + (f" OR [{col.name}] IS NULL" if col.null else "")))
            p += ".HasConversion<byte>()"
        if ty.startswith(("nvarchar", "varchar", "char")):
            length = re.search(r"\((\d+|max)\)", ty).group(1)
            if length != "max":
                p += f".HasMaxLength({length})"
            if not ty.startswith("nvarchar"):
                p += ".IsUnicode(false)"
            if ty.startswith("char"):
                p += ".IsFixedLength()"
        elif ty == "json":
            checks.append((col.name + "_IsJson", f"[{col.name}] IS NULL OR ISJSON([{col.name}]) = 1" if col.null else f"ISJSON([{col.name}]) = 1"))
        elif ty.startswith("decimal"):
            ps = re.search(r"\((\d+),(\d+)\)", ty)
            p += f".HasPrecision({ps.group(1)}, {ps.group(2)})"
        elif ty.startswith("datetime2(") and ty != "datetime2(0)":
            p += f'.HasColumnType("{ty}")'
        elif ty.startswith("varbinary("):
            p += f".HasMaxLength({re.search(r'[(](\d+)[)]', ty).group(1)})"
        if col.default is not None:
            d = col.default
            if isinstance(d, str) and d.startswith("sql:"):
                p += f'.HasDefaultValueSql("{d[4:]}").ValueGeneratedNever()'
            else:
                p += f".HasDefaultValue({cs_literal(d, kind, cst)}).ValueGeneratedNever()"
        if col.check:
            checks.append((col.name, col.check))
        if p.endswith(f"x.{col.name})"):
            continue  # nothing beyond the conventions (required-ness comes from nullable reference types)
        lines.append(p + ";")
    # unique columns
    for col in t.cols:
        if col.uq:
            filt = []
            if soft:
                filt.append("[IsDeleted] = 0")
            if col.null:
                filt.append(f"[{col.name}] IS NOT NULL")
            if col.uq_filter:
                filt.append(col.uq_filter)
            f = f'.HasFilter("{" AND ".join(filt)}")' if filt else ""
            lines.append(f'        builder.HasIndex(x => x.{col.name}).IsUnique(){f}.HasDatabaseName("UQ_{t.name}_{col.name}");')
    for cols, unique, filt, name in t.indexes:
        sel = f"x.{cols[0]}" if len(cols) == 1 else "new { " + ", ".join(f"x.{c}" for c in cols) + " }"
        nm = name or (("UQ_" if unique else "IX_") + t.name + "_" + "_".join(cols))
        f = filt
        if unique and soft:
            f = "[IsDeleted] = 0" + (f" AND {filt}" if filt else "")
        line = f"        builder.HasIndex(x => {sel})"
        line += ".IsUnique()" if unique else ""
        line += f'.HasFilter("{f}")' if f else ""
        line += f'.HasDatabaseName("{nm}");'
        lines.append(line)
    # foreign keys
    for col in t.cols:
        if not col.fk:
            continue
        tcls, tfolder = target(col.fk)
        usings.add(f"SuperApp.Domain.Entities.{tfolder}")
        coll = next((cn for (ch, cname, cn) in children_of.get(col.fk, []) if ch is t and cname == col.name), None)
        with_many = f".WithMany(p => p.{coll})" if coll else ".WithMany()"
        behaviour = {"NoAction": "NoAction", "Cascade": "Cascade", "SetNull": "SetNull", "Restrict": "Restrict"}[col.on]
        lines.append(f"        builder.HasOne(x => x.{nav_name(col)}){with_many}.HasForeignKey(x => x.{col.name})"
                     f".OnDelete(DeleteBehavior.{behaviour});")
    ck = ""
    if checks:
        ck_lines = "\n".join(f'            t.HasCheckConstraint("CK_{t.name}_{s}", "{sql}");' for s, sql in checks)
        ck = f", t =>\n        {{\n{ck_lines}\n        }}"
    head = "\n".join(f"using {u};" for u in sorted(usings))
    text = (f"{head}\n\nnamespace SuperApp.Infrastructure.Persistence.Configurations.{t.folder};\n\n"
            f"/// <summary>Table <c>{t.schema}.{t.name}</c> — see ERD.</summary>\n"
            f"internal sealed class {t.cls}Configuration : IEntityTypeConfiguration<{t.cls}>\n{{\n"
            f"    public void Configure(EntityTypeBuilder<{t.cls}> builder)\n    {{\n"
            f'        builder.ToTable("{t.name}", "{t.schema}"{ck});\n' + "\n".join(lines) + "\n    }\n}\n")
    write(INFRA / "Persistence" / "Configurations" / t.folder / f"{t.cls}Configuration.cs", text)


def emit_enum(e: Enum):
    members = ",\n".join(f"    {m} = {i}" for i, m in enumerate(e.members))
    doc = f"/// <summary>{e.doc}</summary>\n" if e.doc else ""
    text = f"namespace SuperApp.Domain.Enums;\n\n{doc}public enum {e.name} : byte\n{{\n{members},\n}}\n"
    write(DOMAIN / "Enums" / e.folder / f"{e.name}.cs", text)


def main():
    sys.path.insert(0, str(pathlib.Path(__file__).parent))
    sys.modules['generate'] = sys.modules[__name__]  # the spec files register into this module
    import spec_core, spec_pitch_booking, spec_shop_pod, spec_money, spec_rest  # noqa: F401 (they register tables)
    for _ in [cs_type(c, t) for t in TABLES.values() for c in t.cols]:
        pass
    children = {}
    for t in TABLES.values():
        for col in t.cols:
            if col.fk and col.fk not in EXTERNAL and col.collection is not False and (col.on == "Cascade" or col.collection):
                if col.fk == f"{t.schema}.{t.name}":
                    continue
                name = col.collection or t.name
                children.setdefault(col.fk, []).append((t, col.name, name))
    for t in TABLES.values():
        emit_entity(t, children)
        emit_config(t, children)
    for e in ENUMS.values():
        emit_enum(e)
    print(f"tables: {len(TABLES)}  enums: {len(ENUMS)}")


if __name__ == "__main__":
    main()
