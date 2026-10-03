CREATE TABLE normacase.outbound_delivery_receipts (
    delivery_id text NOT NULL,
    destination_id text NOT NULL,
    status integer NOT NULL CHECK (status IN (0, 2)),
    result_json json NOT NULL,
    result_sha256 text NOT NULL CHECK (result_sha256 ~ '^[0-9a-f]{64}$'),
    transport_reference text NULL CHECK (
        transport_reference IS NULL
        OR (length(transport_reference) BETWEEN 1 AND 512
            AND transport_reference !~ '[[:cntrl:]]')
    ),
    PRIMARY KEY (delivery_id, destination_id)
);

CREATE TRIGGER outbound_delivery_receipts_no_mutation
BEFORE UPDATE OR DELETE ON normacase.outbound_delivery_receipts
FOR EACH ROW EXECUTE FUNCTION normacase.reject_audit_trail_mutation();
