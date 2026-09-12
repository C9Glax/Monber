using System.Globalization;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Services.Prices.Fetching;

/// <summary>
/// Parses a REWE "Prospekt" (flyer) PDF for Monster Energy offers. The flyer's page layout is a grid of
/// product tiles; pdfplumber-assisted inspection of live flyer PDFs (rewe_2026_wk37/38_840219) showed each
/// tile's words cluster tightly around the product name's own X position - a "Monster" word's tile
/// (name/pack-size/unit-price lines) sits within roughly 40pt to its right, while the tile's own big
/// display price sits further right (40-130pt), split into two separate word fragments (e.g. "0." and
/// "99" for a superscript-cents price design) that concatenate in reading order into the full price
/// string. Neighbouring tiles/badges sit far enough right (150pt+) that this column split reliably avoids
/// them; the same words are also naturally excluded from the pack-size text (non-numeric) and from the
/// price text (outside the numeric-fragment filter). Confirmed live: matches the flyer's own "je
/// 0,5-l-Dose ... 0,99" tile exactly for the tracked "0,5l" pack size.
/// </summary>
internal static partial class ReweeFlyerParser
{
    // How far below (in PDF points) a tile's words can be from its "Monster" word - calibrated against a
    // real tile spanning name/pack-size/unit-price/deposit lines (~55pt tall).
    private const double TileHeight = 80;
    private const double DescriptionColumnWidth = 45;
    private const double TileColumnWidth = 220;
    private const double LineBucketHeight = 3;

    internal static IReadOnlyList<(string PackSizeSuffix, decimal Price)> ParseMonsterEnergyOffers(byte[] pdfBytes)
    {
        List<(string, decimal)> offers = [];

        using PdfDocument document = PdfDocument.Open(pdfBytes);
        foreach (Page page in document.GetPages())
        {
            Word[] words = [.. page.GetWords()];
            foreach (Word monster in words.Where(w => w.Text == "Monster"))
            {
                double x0 = monster.BoundingBox.Left;
                double top = monster.BoundingBox.Top;

                Word[] tileWords = [.. words.Where(w =>
                    w.BoundingBox.Top <= top + 5 &&
                    w.BoundingBox.Top >= top - TileHeight &&
                    w.BoundingBox.Left >= x0 - 10 &&
                    w.BoundingBox.Left <= x0 + TileColumnWidth)];

                if (!tileWords.Any(w => w.Text == "Energy") || !tileWords.Any(w => w.Text == "Drink"))
                    continue; // Some other "Monster"-branded product, not the energy drink.

                // Adjacent words on the same visual line don't always share the exact same Top (small
                // per-glyph baseline jitter, confirmed live) - round to a coarse line bucket before
                // ordering so same-line words sort left-to-right instead of by that jitter.
                string description = string.Join(" ", tileWords
                    .Where(w => w.BoundingBox.Left <= x0 + DescriptionColumnWidth)
                    .OrderByDescending(w => Math.Round(w.BoundingBox.Top / LineBucketHeight))
                    .ThenBy(w => w.BoundingBox.Left)
                    .Select(w => w.Text));

                Match sizeMatch = PackSizeRegex().Match(description);
                if (!sizeMatch.Success)
                    continue;
                string packSize = sizeMatch.Groups["qty"].Success
                    ? $"{sizeMatch.Groups["qty"].Value}x{sizeMatch.Groups["vol"].Value}l"
                    : $"{sizeMatch.Groups["vol"].Value}l";

                string priceText = string.Concat(tileWords
                    .Where(w => w.BoundingBox.Left > x0 + DescriptionColumnWidth && PriceFragmentRegex().IsMatch(w.Text))
                    .OrderByDescending(w => Math.Round(w.BoundingBox.Top / LineBucketHeight))
                    .ThenBy(w => w.BoundingBox.Left)
                    .Select(w => w.Text));

                if (!decimal.TryParse(
                        priceText.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal price))
                    continue;

                offers.Add((packSize, price));
            }
        }

        return offers;
    }

    [GeneratedRegex(@"je\s+(?:(?<qty>\d+)\s*x\s*)?(?<vol>\d+,\d+)-l")]
    private static partial Regex PackSizeRegex();

    [GeneratedRegex(@"^\d+[.,]?\d*$")]
    private static partial Regex PriceFragmentRegex();
}
