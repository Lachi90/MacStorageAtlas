## 1. Availability Evidence

- [x] 1.1 Confirm the Mac App Store listing is live, and record its id, seller, price, and released version.
- [x] 1.2 Confirm the storefront-neutral URL `https://apps.apple.com/app/id6803195382` resolves to that listing, and record which storefronts already answer.

## 2. Landing Page Prose

- [x] 2.1 Replace the planned-edition wording in the meta description and the Open Graph description of `docs/index.html`.
- [x] 2.2 Replace it in the hero tagline, and point the Mac App Store call to action at the App Store URL.
- [x] 2.3 Replace it in the download options section, the DaisyDisk section, and the LTSoftware section, adding the store link where the section invites a download.
- [x] 2.4 Replace it in the FAQ answer, the download note, and the footer.

## 3. Landing Page Structured Data

- [x] 3.1 Update the structured data description to state that both channels are free and available.
- [x] 3.2 Turn the second offer into an in-stock offer carrying the App Store URL, and add the listing as a `sameAs` reference.
- [x] 3.3 Parse the structured data block and confirm it is valid JSON with the expected offer shape.

## 4. Comparison Table

- [x] 4.1 Replace the price cell of the comparison table with the two available free channels.
- [x] 4.2 Re-verify the competitor price and capability claims against current first-party sources, and re-stamp the verification date only if they still hold.

## 5. README and Supporting Files

- [x] 5.1 Replace the planned-edition wording in the introduction and the download options list of `README.md`, and link the listing.
- [x] 5.2 Update the landing page `lastmod` in `docs/sitemap.xml`.
- [x] 5.3 Record the App Store release in the WP-01 status note of `docs/IMPLEMENTATION_ROADMAP.md`.

## 6. Delivery

- [x] 6.1 Confirm no occurrence of the planned-edition claim survives on any public surface.
- [x] 6.2 Confirm every App Store link on a public surface resolves.
- [x] 6.3 Run `openspec validate --all --strict --no-interactive`.
- [x] 6.4 Run `git diff --check`.
- [x] 6.5 Confirm no application code, test, or packaging file changed.
