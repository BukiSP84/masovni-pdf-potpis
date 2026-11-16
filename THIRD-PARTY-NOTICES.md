# Biblioteke treće strane

Masovni PDF Potpis koristi sljedeće biblioteke. Svaka je pod svojom licencom; kompletni tekstovi licenci nalaze se u izvornim paketima (NuGet) i na stranicama projekata.

| Biblioteka | Verzija | Licenca | Projekat |
|---|---|---|---|
| iText Core (`itext`) | 9.1.0 | GNU AGPL v3 | https://itextpdf.com/ |
| iText Bouncy Castle adapter (`itext.bouncy-castle-adapter`) | 9.1.0 | GNU AGPL v3 | https://itextpdf.com/ |
| Bouncy Castle (`BouncyCastle.Cryptography`) | 2.4.0 | MIT | https://www.bouncycastle.org/ |
| PDFtoImage | 4.1.1 | MIT | https://github.com/sungaila/PDFtoImage |
| SkiaSharp | 2.88.8 | MIT | https://github.com/mono/SkiaSharp |
| PDFium (binarni paketi `bblanchon.PDFium`) | 130.0.6721 | Apache 2.0 / BSD-3-Clause | https://github.com/bblanchon/pdfium-binaries |
| .NET 8 / Windows Forms | 8.0 | MIT | https://github.com/dotnet/runtime |

Zbog iText-a (AGPL v3) i cijela aplikacija se distribuira pod licencom GNU AGPL v3 (vidi `LICENSE`).

Font pečata: aplikacija **ne sadrži** nijedan font. Koristi font koji je već instaliran na računaru korisnika (Myriad Pro iz Adobe Acrobat/Reader instalacije, ili Segoe UI / Arial iz Windowsa) i ugrađuje samo korištene znakove u potpisani PDF.
