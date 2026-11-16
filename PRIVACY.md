# Izjava o privatnosti — Masovni PDF potpis

Posljednja izmjena: 16. novembar 2026. (verzija 1.2.0)

**Masovni PDF potpis ne prikuplja, ne šalje i ne pohranjuje nikakve lične podatke izvan vašeg računara.**

- Aplikacija radi lokalno, na vašem računaru. Ne komunicira s internetom, osim u dva slučaja koja sami uključite:
  - **Vremenski žig** (opcija, zadano isključena). Pri svakom potpisu servisu za vremenski žig koji ste izabrali (zadano DigiCert) šalje se samo kriptografski otisak (SHA-256 hash) potpisa. Sadržaj dokumenta se ne šalje. Tada aplikacija od izdavača vašeg certifikata (OCSP servis naveden u certifikatu) traži i podatak da certifikat nije opozvan.
  - Link na izvorni kod u prozoru „O aplikaciji“ otvara se u vašem pregledniku.
- PDF dokumenti koje izaberete obrađuju se samo na vašem računaru. Ništa se ne šalje autoru niti trećim stranama.
- Za potpisivanje se koristi certifikat iz Windows spremišta certifikata. Privatni ključ ostaje na vašem tokenu/kartici; PIN unosite u prozoru drivera izdavača certifikata, aplikacija ga ne vidi i ne pamti.
- Aplikacija pamti samo svoje postavke (zadnji folder, izabrani certifikat, položaj pečata) u lokalnom fajlu `postavke.json` u vašem korisničkom profilu.
- Dnevnik potpisivanja (`_dnevnik_potpisivanja.csv`) sadrži ime iz certifikata, Windows korisničko ime, ime računara i otiske dokumenata. Sprema se samo lokalno, u folderu s dokumentima, i nikome se ne šalje.
- Nema analitike, praćenja, reklama ni kolačića.

Pitanja: putem [GitHub Issues](https://github.com/BukiSP84/masovni-pdf-potpis/issues).

---

**Privacy policy (English):** Masovni PDF potpis does not collect, transmit or store any personal data outside your computer. All processing happens locally. The only network use is opt-in: if you enable time-stamping, only a SHA-256 hash is sent to the time-stamp server you choose, and the certificate status is checked with your certificate issuer's OCSP service; the private key stays on your token/smart card; the app stores only its own settings locally. No analytics, tracking or ads.
