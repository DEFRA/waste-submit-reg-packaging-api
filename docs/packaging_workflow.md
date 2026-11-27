# The packaging data workflow

What is in place for submitting packaging data, and why it is shaped this way. Read this before changing the Packaging
module (`WasteSubmitRegPackaging.Packaging`).

**This is a sample, not a contract.** It is the whole workflow, built thin, so that each screen the team adds fills in
a step that already has a place. It is expected to change. When a requirement doesn't fit, change the shape and this
page in the same pull request. Don't work round it with a flag, an extra status or a side table.

- [The journey](#the-journey)
- [The three tables](#the-three-tables)
- [The status](#the-status)
- [The next step](#the-next-step)
- [Uploading a file](#uploading-a-file)
- [Validating a file](#validating-a-file)
- [Resubmission and the fee step](#resubmission-and-the-fee-step)
- [What is a placeholder, and what is left out](#what-is-a-placeholder-and-what-is-left-out)
- [Adding a step](#adding-a-step)
- [If the CSV file is replaced by screens](#if-the-csv-file-is-replaced-by-screens)

## The journey

| The user | What the API records |
|---|---|
| Clicks a submission period tile | Nothing. The tile reads `GET /packaging/submissions?...`; a 404 `packaging.not_started` means not started |
| Reads "before you start" and continues | Nothing. It is navigation |
| Starts a file upload | **The submission is created here**, with cycle 1 and a file row |
| Waits while the file is scanned and validated | The uploader's callback records where the file is. Validation then records its result |
| Fixes errors, reads warnings | A new upload is a new file row. Nothing else changes |
| Pays the fee (resubmissions only) | The payment reference, on the cycle |
| Declares and submits | The file submitted, by whom, on the cycle. Status `SubmittedToRegulator` |
| (The regulator decides) | The decision, on the cycle. Status `AcceptedByRegulator` or `RejectedByRegulator` |
| Uploads again after a decision | A new cycle |

**Why the submission is created at the first upload, not before:** until then there is nothing to record, and the
upload is the first moment something outside the user's browser (the uploader) needs a row to report to. No row is
left behind by someone who only looked. EPR does the same today. It is get-or-create: a unique index allows one
submission per submitter and submission period.

## The three tables

| Thing | Table | One row per | Records |
|---|---|---|---|
| Submission (`PackagingSubmission`) | `pkg_packaging_submission` | Submitter and submission period | Who, which period, and the one status |
| File (`SubmissionFile`) | `pkg_submission_file` | Upload | The file name, where it is in S3, and what validating it found |
| Cycle (`SubmissionCycle`) | `pkg_submission_cycle` | Pass from upload to the regulator's decision | The file submitted, by whom, the payment reference, the decision |

- The **submitter** is an organisation, plus a compliance scheme id when a scheme submits for its members.
- The **cycle** is the unit that repeats. Cycle 1 is the first submission. A resubmission is cycle 2, 3 and so on, so
  it never overwrites what went before, and the rows are the history: there is no separate history table.
- The three are one aggregate. They are always loaded and saved together, and every change goes through a method on
  `PackagingSubmission`.

## The status

One status, on the submission, in the terms EPR uses today:

```
(no row)             --start upload-->  InProgress            (cycle 1)
InProgress           --submit-->        SubmittedToRegulator
SubmittedToRegulator --Accepted-->      AcceptedByRegulator
SubmittedToRegulator --Rejected-->      RejectedByRegulator
Accepted / Rejected  --start upload-->  InProgress            (the next cycle)
```

There is no status for "file uploaded", "has errors" or "fee paid". Those are facts on the file and the cycle, and
the next step is worked out from them.

## The next step

`PackagingSubmission.NextStep()` says what the producer has to do next. Every read and every command returns it as
`nextStep`, and **the frontend routes on it**. The frontend never works the step out from other fields.

| `nextStep` | When | Which cycles |
|---|---|---|
| `UploadFile` | The last file was rejected by the uploader | Every cycle |
| `AwaitFileCheck` | A file is uploading, or stored and being validated | Every cycle |
| `FixErrors` | The latest file has errors | Every cycle |
| `PayResubmissionFee` | The latest file is valid and no payment reference is recorded | Resubmissions only |
| `SubmitToRegulator` | The latest file is valid, and on a resubmission the payment reference is recorded | Every cycle |
| `null` | Nothing for the producer to do. The status says why | |

- A producer step can only be taken when it is the next step; otherwise the API answers 409
  `packaging.step_not_available`. So the order of the journey is enforced in one place.
- A new upload is allowed at any point in an open cycle, whatever the next step is.
- Warnings don't block. The view carries the latest file's `warningCount`, and the frontend shows the warnings before
  the submit page.
- A first submission uses three or four of the five values. `PayResubmissionFee` only appears after an earlier cycle
  was accepted.

## Uploading a file

Files are uploaded through the [CDP uploader](https://portal.cdp-int.defra.cloud/documentation/how-to/file-upload.md),
which scans them and puts clean ones in S3. It tells this API the result with its callback.

```
1. Frontend -> API        POST /packaging/submissions/file-uploads       (API key and producer headers)
                          <- packagingSubmissionId, fileId, callbackPath
2. Frontend -> uploader   POST /initiate  { s3Bucket, redirect, callback: <this API's URL> + callbackPath }
                          <- uploadUrl
3. Browser  -> uploader   the user posts the file to uploadUrl. The form's file input is named "file"
4. Uploader -> API        POST <callbackPath>  { form: { file: { fileStatus, filename, s3Bucket, s3Key } } }
5. Frontend -> API        GET /packaging/submissions/{id}, until nextStep is no longer AwaitFileCheck
```

- **How the callback finds its submission:** the callback path is made for one upload and contains this API's file id
  (`/packaging/file-uploads/{fileId}/scan-result/{token}`). A file row belongs to one submission and one cycle.
- **How the callback is protected:** the uploader sends no API key and no user. The path carries a random token, made
  at step 1 and stored on the file row; without it the answer is 404. This is the one endpoint with no read or write
  policy, and `EndpointConventionsTest` lists it by name. The callback path is a secret: the frontend passes it to the
  uploader and nowhere else.
- **A repeated callback changes nothing.**
- **The callback only records what the uploader said.** A stored file becomes `Validating`. It is not validated in
  the callback: see [Validating a file](#validating-a-file).
- The file name and its place in S3 arrive with the callback, not at step 1.
- The user who uploaded is recorded at step 1, because the callback has none.
- This API never calls the uploader or S3. The frontend calls `/initiate`.

To confirm with the CDP team before relying on it: that the callback can reach this service in each environment, and
that it sends no credentials.

## Validating a file

**Validation is asynchronous by design.** It is a step of its own, after the upload and apart from it, with its own
state. A file can be large and has to be read from S3, so nothing assumes validation finishes inside a request.

```
Uploading  --the uploader stored it-->   Validating  --validated-->  Valid | Invalid
Uploading  --the uploader rejected it--> Rejected
```

- **Two commands, because they are two events:**
  - `RecordPackagingFileScanResult` is the uploader's callback. It records the file and sets `Validating`.
  - `ValidatePackagingFile` takes a file that is `Validating`, runs `IPackagingFileValidator`, and records the result:
    `Valid` or `Invalid`, with `error_count`, `warning_count` and `validated_at`. Nothing else sets those statuses.
- **A caller only ever waits.** While the file is `Uploading` or `Validating` the next step is `AwaitFileCheck`. The
  caller re-reads `GET /packaging/submissions/{id}` until the next step moves on, and doesn't need to know which of
  the two it is waiting for.
- **What starts validation today is a placeholder.** The callback endpoint calls `ValidatePackagingFile` straight
  after recording the scan result (one marked line in `PackagingSubmissionEndpoints.cs`), because the placeholder
  validator is instant. So today a caller will rarely see `Validating`.
- **When validation is real:** delete that line and have a background worker call the same command for files that are
  `Validating`. No status, table or endpoint changes. The worker's own design (how it finds files, locking between
  instances, retries, a file that can't be read) is to be decided then, with the real validator in front of it.

### Errors and warnings

The result recorded on the file row is all the workflow needs: the next step depends on it.

**Not decided: whether each error and warning is stored.** They are not stored today. When the error and warning
screens are built, decide between:

- **Storing them** (one more table, written once per file by `ValidatePackagingFile`, capped as EPR caps at 1,000).
  The screens can page through them, the user can come back tomorrow, and the file is read from S3 once.
- **Returning them without storing.** This doesn't fit asynchronous validation: there is no request to return them
  in, so the file would have to be read and validated again for each page.

## Resubmission and the fee step

- A cycle is a **resubmission** when an earlier cycle was accepted. This is EPR's rule today. A cycle that follows a
  rejection is not a resubmission.
- Only a resubmission has a fee step. On a resubmission the next step after a valid file is `PayResubmissionFee`, and
  the cycle can't be submitted until that step is done.
- **The step is a placeholder.** `POST /packaging/submissions/{id}/resubmission-fee-payment` records a
  `paymentReference` on the cycle, and that is all. No fee is worked out and no payment is taken.
- **What fills it in:**
  - The fee is worked out by the Fees module (`IFeesApi`) from what the file says: the regulator, the members. It
    is not taken from the caller.
  - The payment is captured in the payment table ported from epr-payment-service. The cycle's `payment_reference` is
    the link to it.
  - See "Fees in a workflow" in [project_architecture.md](project_architecture.md) for the pattern.
- Fill this step in. Don't add a second one: where the fee sits in the workflow is already settled.

## What is a placeholder, and what is left out

| | Today | Where it goes when built |
|---|---|---|
| Validating the file | `PlaceholderPackagingFileValidator` passes every file | Replace the class behind `IPackagingFileValidator` |
| Starting validation | The callback endpoint calls `ValidatePackagingFile` | A background worker calls the same command |
| The errors and warnings themselves | Not stored | See [Errors and warnings](#errors-and-warnings) |
| The resubmission fee | Not worked out | `RecordPackagingResubmissionFeePayment`, through `IFeesApi`, from what the file says |
| The payment | A reference on the cycle | The payment table ported from epr-payment-service |
| The resubmission reference number (`PEPR…`) | Not there | A column on the cycle, set when the cycle opens |
| Is the period open for this submitter | Not checked | `StartPackagingFileUpload`, through `ISubmissionPeriodsApi` |
| Is the regulator the right nation | Not checked | `RecordPackagingRegulatorDecision` |
| Uploading while the regulator is deciding | Refused (409). EPR allows it | A team decision; `PackagingSubmission.StartFileUpload` |

A first submission needs no switch to skip errors, warnings and payment: the placeholder passes every file, and a
first submission has no fee step, so the next step after the upload is `SubmitToRegulator`.

## Adding a step

1. Say what the step records, and on which of the three things: the submission, a file or a cycle.
2. Add a method to `PackagingSubmission` that checks it is allowed, records it and returns a `Result`.
3. Add a `PackagingNextStep` value **only if the producer has to do something new**, and place it in `NextStep()`.
4. Add the command and handler (one folder under `Application/Commands/`), the request and the endpoint.
5. Add a changeset for any new column, and its mapping in `PackagingDbContext.cs`.
6. Add tests: the rule in `UnitTests/Packaging/PackagingSubmissionTest.cs`, the endpoint in
   `ServiceTests/Packaging/PackagingSubmissionEndpointsTest.cs`.
7. Update this page.

## If the CSV file is replaced by screens

Nothing is built for this. The shape allows for it: a cycle submits a file today, and data entered on screens would be
another source of what a cycle submits. The submission, the cycle, the status and the next step don't depend on the
data being a file.
