# 0001: Fees and submission periods are shared modules

## Context

The packaging workflow in CDP needs the packaging resubmission fee and the packaging submission periods.
Later, registration moves here from epr-payment-service with its own fees, payments and submission periods.

- The fee rates, the resubmission calculation and the registration submission periods are in
  epr-payment-service (Azure SQL Server, `[Lookup]` schema).
- The packaging submission periods are held only in config:
  - the packaging frontend's `SubmissionPeriods`;
  - the POM validation function's `SubmissionPeriods`, with its period codes.

  Nothing links a packaging period to a registration period.

If packaging built these its own way, the registration move would have to rework or duplicate them.

## Decision

1. **There are two shared modules**, each with its own tables, changelog folder and Contracts project:
   - **Fees** (`fee_*`, `IFeesApi`);
   - **SubmissionPeriods** (`period_*`, `ISubmissionPeriodsApi`).

   They don't depend on each other or on any workflow module.
2. **It's shared:**
   - any module may join to the `fee_*` and `period_*` tables and hold foreign keys to them;
   - in C#, modules use `IFeesApi` and `ISubmissionPeriodsApi`;
   - only Liquibase changesets change the data, and the module's DbContext is read-only.
3. **It keeps epr-payment-service's ids and meanings** (Group, SubGroup, Regulator, RegistrationFees, SubmissionPeriod):
   - amounts stay in pence, `numeric(19,4)`;
   - rate periods are inclusive dates;
   - registration rates arrive later as new rows, and registration data copies with no conversion.
4. **Each packaging submission period links to one registration submission period** (a registration year).

   **A submission period and a data period are different things.** A submission period is when producers
   submit ("July to December 2025"). A data period is the time the data covers, identified by the code in the
   POM file: 2025-H2 is July to December, and 2025-P0 is the whole of 2025. A submission period accepts one or
   more data periods, and each data period has its own description, dates and producer size (large
   producers report H1 and H2, small producers P0). So there are two tables: `period_packaging_submission_period`,
   one row per submission period with its dates (visible from, opening, deadline), and `period_packaging_data_period`,
   one row per code with a foreign key to the submission period that accepts it. The dates belong to the submission
   period only: in EPR they don't vary by data period, producer size or submitter type. This brings together the
   packaging frontend's and POM validation function's config, epr-data-sqldb's `v_PoM_Codes` and the calculator's
   `submission_period_lookup`.

   **No validation error codes in the period tables.** The POM validation function's per-period codes (54 to 89)
   were its own config. Validation becomes a project in this service, which checks a file's period codes against
   the chosen period's data periods and the producer's size, and owns its error codes in code.
5. **The resubmission fee calculation is ported unchanged.** It's a pure calculator that returns fee lines:
   - a producer pays the flat rate;
   - a compliance scheme pays the rate × members, with at least 1 member;
   - outstanding = total − previous payments, not clamped;
   - the rate is matched on the date part, inclusive, and the latest effective-from wins.

   Parity tests use the payment service's cases and the seeded rates.
6. **Workflows store what they charge.** A fee snapshot and line items belong to the workflow module, saved
   with its status change, as in epr-payment-service's registration fee snapshot. Fees stores nothing
   per submission.

## Consequences

- A new rate or period is a changeset, with no code change.
- Registration drops in as one more module that uses the same tables and contracts.
- Overlapping rates are caught by a test, not by the database, because `btree_gist` isn't enabled in CDP.
  Ask CDP if a database constraint is wanted.
