## Context

`resolve-app-store-rejection` prepared the App Store edition and described it
honestly as planned, because the submission was still open. Apple has since
released 1.0.3, so the same honest description is now the wrong one. The work is
a wording sweep, but it touches the two surfaces that machines read: the
structured data of the landing page and the comparison table that
`product-comparison` governs.

## Goals / Non-Goals

**Goals:**

- One consistent availability claim across the landing page, the README, and
  the structured data.
- A rule that makes the next channel change a spec-checkable edit rather than a
  discovery.
- A Store link that works for any visitor's storefront.

**Non-Goals:**

- Publishing a new version, building, or uploading anything. The live build is
  1.0.3 and stays 1.0.3.
- Editing the App Store listing metadata, which lives in App Store Connect and
  not in this repository.
- Adding Apple's "Download on the Mac App Store" badge artwork.
- Restructuring the landing page, the comparison table, or the download flow.
- Changing `docs/privacy.html` or `docs/support.html`, which already describe
  the App Store as an existing channel.

## Decisions

**The Store link is storefront-neutral.**
`https://apps.apple.com/app/id6803195382` redirects each visitor into their own
storefront. Hard-coding `/us/` or `/de/` would send every visitor to one
country's page, which shows a wrong price format at best and an unavailable
product at worst. The country-neutral form also survives a change in the set of
countries the app is sold in.

**GitHub stays the primary call to action.**
The project is open source and its own release channel is the one it controls
end to end, including checksums and release notes. The Store becomes an equal
second option rather than the headline, which also keeps the existing button
hierarchy of the page intact.

**No Apple badge artwork.**
Apple's badge carries its own marketing requirements for size, clear space, and
modification, and the page already has a consistent button system. A text
button in the existing style is less to maintain and cannot fall out of
compliance.

**The structured data models two offers, not one.**
The application is free through two distinct channels with different URLs, so
each channel keeps its own `Offer`. The App Store offer changes from
`PreOrder` without a URL to `InStock` with the listing URL, which is exactly
the claim the prose now makes.

**The requirement lands in `release-packaging`.**
That capability already owns user-facing download documentation, including the
rule that separates unsigned development builds from signed public artifacts.
Availability of a download channel is the same kind of claim.
`product-identity` was rejected as a home because it is about the name the
product carries, and a new capability was rejected as disproportionate for one
requirement about existing documentation.

## Risks / Trade-offs

- **The listing is not yet answering in every storefront.** At the time of
  writing, the product page resolves in the US, the United Kingdom,
  Switzerland, and Japan, and returns 404 in Germany, Austria, and France.
  Storefront propagation after a first release commonly lags by hours → The
  page makes no country-specific claim, and the storefront-neutral link routes
  each visitor to their own store. Before publishing, the operator re-checks
  the storefronts that matter and, if a storefront is still missing after
  propagation, verifies the country selection in App Store Connect.

- **The App Store URL is a hard dependency on an external id.** A broken link
  is worse than no link → The delivery tasks verify that the URL resolves, and
  the id is written once per surface so a future change is a small edit.

- **Announcing availability in the comparison table restarts its verification
  clock.** The `product-comparison` requirement about primary evidence applies
  to the whole table, not to the edited row → The verification date is
  re-stamped only after the competitor claims in the table are confirmed to be
  unchanged and their sources still current.
