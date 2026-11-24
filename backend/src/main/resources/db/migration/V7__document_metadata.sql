-- Ordnung der Fallakte wie in der Begutachtung: Art des Dokuments und Ausstellungsdatum, dazu die Herkunft einzelner
-- Angaben (welche Angabe steht auf welcher Seite welches Dokuments). Alles technische Kennungen ohne Fallinhalt;
-- die Zeilen hängen am Dokument und gehen mit ihm.
ALTER TABLE documents.document
    ADD COLUMN category  text NOT NULL DEFAULT 'OTHER'
        CHECK (category IN ('ORDER', 'MEDICAL', 'CARE', 'PROOF', 'OTHER')),
    ADD COLUMN issued_on date;

CREATE TABLE documents.reference (
    process_id   uuid    NOT NULL,
    document_id  text    NOT NULL,
    field_id     text    NOT NULL CHECK (field_id ~ '^[a-z][a-z0-9_]{0,63}$'),
    page         integer NOT NULL CHECK (page >= 1),
    PRIMARY KEY (process_id, document_id, field_id, page),
    FOREIGN KEY (process_id, document_id) REFERENCES documents.document ON DELETE CASCADE
);
