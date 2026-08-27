## Why

App Review approved 1.0.3 and the Mac App Store listing is live as a free
Utilities app under the seller Lachmann Thiem Software GbR, reachable at
`https://apps.apple.com/app/id6803195382`.

Every public surface still describes that edition as planned. Fifteen passages
of `docs/index.html` and two of `README.md` announce a "planned Mac App Store
edition", the hero button that promises the Store scrolls to an anchor instead
of leading anywhere, and the landing page structured data publishes the Store as
a `PreOrder` offer with no URL. A search engine or a store aggregator reading
that page is therefore told the app cannot be installed from the App Store,
which is now false.

The wording was correct while the submission was pending. Nothing keeps it from
becoming stale again after the next channel change, because no specification
ties an availability claim to the state of the channel it describes.

## What Changes

- Establish that public surfaces describe a distribution channel as available
  only while that channel is live, link every channel they present as
  available, and keep the landing page structured data in step with the same
  claim.
- Replace the planned-edition wording with availability wording across the
  landing page and the README, and point the Mac App Store call to action at
  the storefront-neutral App Store URL.
- Correct the landing page structured data: the second offer becomes an
  in-stock offer carrying the App Store URL, and the software application gains
  the listing as a `sameAs` reference.
- Re-stamp the verification date of both comparison tables, because their own
  distribution rows change.
- Refresh the sitemap entry for the landing page and record the release in the
  WP-01 status note of the roadmap.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `release-packaging`: add a requirement that public download surfaces present
  only live distribution channels as available, link them, and describe a
  prepared but unreleased channel as planned.

## Impact

- `docs/index.html`: meta description, Open Graph description, structured data
  description and offers, hero tagline and call to action, download options,
  the DaisyDisk and LTSoftware sections, the comparison price row and its
  verification date, the FAQ, the download note, and the footer.
- `README.md`: the introduction, the download options list, the distribution
  row of the comparison table, and its verification date.
- `docs/sitemap.xml`: the `lastmod` of the landing page.
- `docs/IMPLEMENTATION_ROADMAP.md`: the WP-01 status note.
- No application code, tests, packaging, entitlements, or App Store metadata
  change. The app itself is untouched; this change is about how its channels
  are described.
