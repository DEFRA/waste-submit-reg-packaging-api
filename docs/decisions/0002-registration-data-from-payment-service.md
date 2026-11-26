# 0002: Registration data from epr-payment-service keeps its ids

## Context

- **Registration data will be migrated into this database** from epr-payment-service. It has two parts:
  - the registration submission periods, `[Lookup].[SubmissionPeriod]`, ids 1 to 12;
  - the registration submission data, `[registration].[RegistrationSubmissionData]` and its child tables.
- **`RegistrationSubmissionData.SubmissionPeriodId`** is a NOT NULL foreign key to those ids.
- **`dbo.Payment.RegistrationSubmissionDataId`** links payments to the data rows by their uuid.
- **The registration module isn't built here yet.** The submission periods already are: they're
  `period_registration_submission_period` in the SubmissionPeriods module.

## Decision

1. **The registration submission period ids and values are the payment service's, unchanged.**
   - **What's copied:** `period_registration_submission_period` has the same id, window type, registration year and opening,
     deadline and closing dates as `[Lookup].[SubmissionPeriod]`.
   - **A test pins this:** `SubmissionPeriodsDatabaseTest.The_registration_submission_periods_keep_the_payment_services_ids_and_values`
     checks all 12 rows.
   - **How ids are kept:**
     - they're copied, never generated: a plain `integer` key, with no identity;
     - a new one is a changeset with an explicit id (the next is 13), added in step with the payment service
       until registration moves;
     - an id is never renumbered or reused.
2. **The registration submission data keeps its keys when it moves.** The future `reg_*` tables take the rows as
   they are:

   | epr-payment-service | Here |
   |---|---|
   | `[Lookup].[SubmissionPeriod]` (every column) | `period_registration_submission_period`, the same row: id, window type, registration year, dates |
   | `RegistrationSubmissionData.Id` (uuid) | The `reg_*` data table's id, the same value |
   | `RegistrationSubmissionData.SubmissionPeriodId` | Kept, with a foreign key to `period_registration_submission_period (id)` |
   | `SubmissionId`, `RegistrationBlobName` (unique), `ApplicationReferenceNumber` | Kept as columns, because they're the natural keys |
   | Producer, subsidiary, event, fee snapshot and line item ids (uuids) | The same values |
   | `Payment.RegistrationSubmissionDataId` | The same value, so payment links still resolve |

3. **Copy the rows, don't replay them.**
   - The payment service's own `RegistrationHistoryMigrationRunner` rebuilds submissions through its handlers, so
     every id is new. It isn't the model for this move.
   - The move copies each row with its ids, in foreign key order: submission periods first (already seeded), then data, then
     children, then payments.

## Consequences

- Fee snapshots, line items and payments can move later without any id mapping.
- **Until registration moves, the two window tables must be kept in step:** a window added in the payment service
  needs the same changeset here. The parity test fails if the seeded values drift.
- The migration route (a streaming export from the payment service into staging tables) is covered in the session
  notes. This note only fixes what must not change.
