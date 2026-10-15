# Communities — version 1

Agreed with the user on 2026-10-15:
- forum only (posts and comments), **no chat**;
- only Super App creates communities;
- freelancer services come later.

**Resume rule:** if the session stops, continue from the first unticked item and don't redo finished items.

## Checklist
- [ ] 1. Data and service (`js/services/communityService.js`):
  - communities with an audience: everyone / sellers / Pro+ sellers;
  - posts: text, image, poll; comments; reactions; pinned posts and announcements;
  - joining; badges (seller level, Super App team, store owner);
  - reports; banned-word filter; slow mode; muted members;
  - notifications when someone comments on your post;
  - seed communities and posts.
- [ ] 2. Site:
  - `#/community` (your feed + discover);
  - `#/community/:slug` (community page, composer, feed, comments, report);
  - `#/community/:slug/:postId` (one post);
  - links in the menu, footer and profile.
- [ ] 3. Admin (`#/admin/community`):
  - reports queue (hide / keep / mute author);
  - communities (create / edit / archive, slow mode, audience, rules);
  - all posts (search, hide, pin, announce, delete, close comments);
  - muted members;
  - banned words;
  - post as Super App;
  - to-do badge.
- [ ] 4. Sharaf test section links; Arabic strings; mobile, dark mode and error check; README; push.
