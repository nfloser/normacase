CREATE TABLE normacase.outbound_delivery_receipts (
    delivery_id text NOT NULL,
    destination_id text NOT NULL,
    result_json json NOT NULL,
    result_sha256 text NOT NULL CHECK (result_sha256 ~ '^[0-9a-f]{64}$'),
    status text NOT NULL CHECK (status IN ('Delivered','Rejected')),
    transport_reference text,
    PRIMARY KEY(delivery_id,destination_id)
);
CREATE TRIGGER outbound_receipts_no_mutation BEFORE UPDATE OR DELETE
ON normacase.outbound_delivery_receipts FOR EACH ROW
EXECUTE FUNCTION normacase.reject_audit_trail_mutation();
