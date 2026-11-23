-- Meine Ablage wie im bisherigen Fallexplorer: eigene Ordner, Farbmarkierung, Lesezeichen, Notizen und
-- Arbeitslistenfarben je Konto. Keine Fallinhalte: Markierungen hängen am Vorgang und werden mit ihm gelöscht;
-- Notizen (Freitext) zusätzlich schon bei der Quittung durch das Fachsystem.
CREATE TABLE workflow.personal_folder (
    folder_id   uuid PRIMARY KEY,
    owner       text        NOT NULL,
    parent_id   uuid        REFERENCES workflow.personal_folder ON DELETE CASCADE,
    label       text        NOT NULL CHECK (length(label) BETWEEN 1 AND 80),
    color       text        NOT NULL CHECK (color ~ '^#[0-9a-f]{6}$'),
    created_at  timestamptz NOT NULL
);
CREATE INDEX personal_folder_owner_idx ON workflow.personal_folder (owner);

CREATE TABLE workflow.personal_mark (
    process_id  uuid    NOT NULL REFERENCES workflow.process ON DELETE CASCADE,
    owner       text    NOT NULL,
    folder_id   uuid    REFERENCES workflow.personal_folder ON DELETE SET NULL,
    color       text    CHECK (color IS NULL OR color ~ '^#[0-9a-f]{6}$'),
    bookmark    boolean NOT NULL DEFAULT false,
    note        text    CHECK (note IS NULL OR length(note) <= 1000),
    PRIMARY KEY (process_id, owner)
);
CREATE INDEX personal_mark_owner_idx ON workflow.personal_mark (owner);

CREATE TABLE workflow.personal_queue_color (
    owner  text NOT NULL,
    queue  text NOT NULL,
    color  text NOT NULL CHECK (color ~ '^#[0-9a-f]{6}$'),
    PRIMARY KEY (owner, queue)
);
