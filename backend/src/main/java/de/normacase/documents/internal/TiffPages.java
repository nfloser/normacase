package de.normacase.documents.internal;

import de.normacase.documents.InvalidDocumentContentException;
import java.awt.image.BufferedImage;
import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.util.ArrayList;
import java.util.Iterator;
import java.util.List;
import javax.imageio.ImageIO;
import javax.imageio.ImageReader;
import javax.imageio.stream.ImageInputStream;

/**
 * Seitenvorschauen für TIFF-Scans (z. B. Fax-Eingänge). Browser zeigen TIFF nicht an; NormaCase wandelt jede Seite
 * mit dem TIFF-Leser des JDK in PNG um. Vor dem Dekodieren werden Seitenzahl und Bildgröße geprüft, damit ein
 * präpariertes Bild nicht den Speicher füllt. Der Inhalt wird nicht ausgewertet.
 */
final class TiffPages {

    static {
        ImageIO.setUseCache(false);
    }

    private TiffPages() {
    }

    static List<byte[]> render(String documentId, byte[] tiff, int maxPages, long maxPixels) {
        try (ImageInputStream in = ImageIO.createImageInputStream(new ByteArrayInputStream(tiff))) {
            Iterator<ImageReader> readers = ImageIO.getImageReadersByFormatName("tiff");
            if (in == null || !readers.hasNext()) {
                throw new InvalidDocumentContentException(documentId, "TIFF kann nicht gelesen werden");
            }
            ImageReader reader = readers.next();
            try {
                reader.setInput(in, false, true);
                int count = reader.getNumImages(true);
                if (count < 1 || count > maxPages) {
                    throw new InvalidDocumentContentException(documentId, "TIFF mit 1 bis " + maxPages + " Seiten erwartet");
                }
                List<byte[]> pages = new ArrayList<>(count);
                for (int i = 0; i < count; i++) {
                    long pixels = (long) reader.getWidth(i) * reader.getHeight(i);
                    if (pixels <= 0 || pixels > maxPixels) {
                        throw new InvalidDocumentContentException(documentId, "TIFF-Seite ist zu groß");
                    }
                    BufferedImage image = reader.read(i);
                    ByteArrayOutputStream png = new ByteArrayOutputStream();
                    if (!ImageIO.write(image, "png", png)) {
                        throw new InvalidDocumentContentException(documentId, "TIFF-Seite kann nicht umgewandelt werden");
                    }
                    pages.add(png.toByteArray());
                }
                return pages;
            } finally {
                reader.dispose();
            }
        } catch (IOException | RuntimeException e) {
            if (e instanceof InvalidDocumentContentException invalid) {
                throw invalid;
            }
            throw new InvalidDocumentContentException(documentId, "TIFF kann nicht gelesen werden");
        }
    }
}
