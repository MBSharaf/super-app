# Window 2 — Design system and content

**Job:** keep the Super App brand and design system and write content (site texts, campaign copy,
post visuals) that matches it. This window owns the design system artifact.

## The design system

- **Artifact:** "Super App" — https://claude.ai/artifact/65e1JyQRGpGN3JMgobxF2f (a Design System artifact, 77 files).
- **How any window reads it:** with the Artifact tool, `read` the URL with `path: "project/README.md"` first, then `project/tokens.json` and `project/components/<Name>/README.md`. Never guess values.
- **Built from:** the site's real styles (`css/tokens.css`, `css/components.css`, …).

## Brand in short (from the site's code; the artifact is the full source)

| | |
|---|---|
| Ink (text, primary buttons) | `#0D1411` |
| Turf green (availability, pitch, success) | `#0B6E4F` |
| Blaze orange (the one action accent) | `#FF5B14` |
| Gold (prizes and premium leagues only) | `#C9920F` |
| Fonts | Archivo (Latin), Cairo (Arabic) |
| Voice | Short, friendly, Egyptian Arabic for Arabic texts; plain English for English UI |
| Modes | Light and dark; English left-to-right and Arabic right-to-left |

## Handoff from the design-system chat

The design-system work was done in a separate cloud chat (claude.ai/code/session_01KdUXaQ8kdj6zjJLXkhVui9).
Other windows cannot read that chat, so its knowledge must be written here. In that chat, ask it to fill in the
section below (and push). Until then the artifact above is the source of truth.

> **To paste into the design-system chat:**
> «اكتب ملخص كامل لكل اللي عملناه في المحادثة دي: القرارات، ألوان وخطوط ومكونات الـ design system، أي محتوى اتولد (نصوص، بوستات، صور) وفين متخزن، واللي لسه ناقص. حطه في ملف `docs/context/design-system.md` تحت عنوان "Handoff" في ريبو super-app واعمله push. ماتحطش أي باسوردات أو بيانات شخصية.»

### Handoff
- *(to be filled by the design-system chat)*

## Inbox (requests from other windows)
- *(empty)*

## Done
- *(empty)*
