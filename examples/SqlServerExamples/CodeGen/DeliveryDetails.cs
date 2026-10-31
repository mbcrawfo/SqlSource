namespace SqlServerExamples.CodeGen;

// The carrier's details of a delivery, the class that Deliveries.sql gives the json column details with its markers.
// The generated methods read and send it through System.Text.Json, whose default options match the JSON's keys to
// these names, case and all: {"Carrier": "Parcelforce", "TrackingNumber": "PF1234567"}.  See docs/manual/settings.md,
// "A class in a JSON column".
public sealed record DeliveryDetails(string Carrier, string TrackingNumber);
