
Printuesi — Epson Label Printing Automation

A web application for preparing, previewing, printing, and tracking PDF product labels for an Epson ColorWorks CW-C6000AE printer. It combines a React interface, an ASP.NET Core API, a PostgreSQL database, and a Linux/CUPS print queue.

Printuesi supports manual PDF printing and order-based automatic printing. Its workflows are built around paper changes, printer setup, test prints, and traceability of generated labels.

    Project status: Active development. The README describes the architecture and workflows established for this project. Some controls (especially fine-grained audit logging, driver-preset editing, and retention rules) are still being refined; verify them in the deployed code before production use.

<!-- GITHUB SCREENSHOT SLOT: replace the block below with: ![Printuesi dashboard](docs/images/dashboard.png) -->

    📸 Screenshot placeholder — Dashboard
    Save a screenshot as docs/images/dashboard.png and replace this block with ![Printuesi dashboard](docs/images/dashboard.png).

Contents

    Features
    Technology stack
    System architecture
    Project structure
    Prerequisites
    Local development
    Configuration
    Printing workflows
    PDF templates and placeholders
    Users, permissions, and audit
    Selected API endpoints
    Printer and CUPS diagnostics
    Troubleshooting
    Generated files and retention
    Screenshots to add
    Development roadmap
    Security and licensing

Features
Area	Description
Dashboard	Printer connectivity, job counts, recent jobs, and quick actions.
Automatic printing	Upload an order PDF, identify label items, group them by paper size, and print one group at a time.
Manual printing	Select a client/template, enter values such as EXP/LOT/KG where applicable, preview the generated PDF, and print.
Job Control	Inspect paper groups, requested/printed quantities, statuses, and generated PDF previews; control test, start, stop, and cancel operations.
Clients	Load clients from PostgreSQL and view their print-job history.
Users	Administrator-facing user creation, role assignment, and password management.
PDF processing	Replace text placeholders such as {{EXP}}, {{LOT}}, and (where supported by the template) {{KG}}.
Print archive	Retain generated PDFs per job and date for review and traceability.
Printer settings	Paper-specific Epson settings are currently applied through the printing workflow; a UI-driven preset editor is a planned improvement.
Primary pages
<!-- GITHUB SCREENSHOT SLOT: automatic printing -->

    📸 Screenshot placeholder — Automatic Printing: docs/images/automatic-printing.png

<!-- GITHUB SCREENSHOT SLOT: manual printing -->

    📸 Screenshot placeholder — Manual Printing: docs/images/manual-printing.png

<!-- GITHUB SCREENSHOT SLOT: job control -->

    📸 Screenshot placeholder — Print Jobs / Job Control: docs/images/print-jobs.png

<!-- GITHUB SCREENSHOT SLOT: clients -->

    📸 Screenshot placeholder — Clients and Print History: docs/images/clients.png

<!-- GITHUB SCREENSHOT SLOT: users -->

    📸 Screenshot placeholder — User Management: docs/images/users.png

Technology stack

Frontend

    React with Vite
    React Router
    JavaScript / JSX
    CSS and Bootstrap-style UI classes

Backend

    ASP.NET Core / .NET 10 (C#)
    Entity Framework Core with PostgreSQL/Npgsql
    JWT bearer authentication and role-based authorization
    iText for PDF text processing
    Swagger / Swashbuckle for development API documentation

Printing infrastructure

    Epson ColorWorks CW-C6000AE
    Linux virtual machine running CUPS
    Epson CUPS driver and a queue named EpsonLabel in the current setup
    Printer reached through LPD (the setup tested with PASSTHRU)
    Shared/network label-template library (mapped as W:\ on the Windows development machine)

    Package versions and available commands can change. The .csproj and package.json files in the repository are the source of truth for the exact versions in use.

System architecture
Operator / AdministratorReact + Vite frontendASP.NET Core backendPostgreSQLShared original PDF templatesiText PDF generatorGenerated PDF archiveLinux VM + CUPS: EpsonLabelEpson CW-C6000AEJWT / REST APIRemote print commands / PDFjobsLPD

Important distinction: PostgreSQL normally stores label metadata, job details, and template paths. The artwork itself is a PDF in the shared template directory. A successful DB query does not guarantee that the backend is reading a recently updated PDF file.
Project structure

Representative locations in the current solution (some names may differ slightly in your checkout):
Printuesi/
├── Printuesi.Server/
│   ├── Controllers/              # API endpoints
│   │   ├── PrintJobsController.cs
│   │   ├── ManualPrintingController.cs
│   │   ├── ClientsController.cs
│   │   └── UsersController.cs
│   ├── Services/                 # Business logic and service interfaces
│   │   ├── PrintJobService.cs
│   │   ├── ManualPrintingService.cs
│   │   ├── PdfService.cs
│   │   ├── PrinterService.cs
│   │   ├── ClientService.cs
│   │   ├── UsersService.cs
│   │   └── OutputCleanupService.cs
│   ├── Data/                     # ApplicationDbContext
│   ├── Entities/                 # EF Core entities
│   ├── Enums/                    # Status, PaperSize, roles, ...
│   ├── Migrations/
│   ├── wwwroot/
│   │   ├── fonts/                # Licensed font supplied locally
│   │   └── labels_output/        # Generated PDFs; do not commit
│   ├── Program.cs
│   └── Printuesi.Server.csproj
├── printuesi.client/
│   ├── src/
│   │   ├── api/                  # http.js and API wrappers
│   │   ├── components/
│   │   ├── contexts/             # Authentication, language, theme
│   │   └── pages/                # Dashboard, ManualPrinting, PrintJobs, ...
│   ├── vite.config.js
│   └── package.json
├── docs/
│   └── images/                  # Screenshots for this README
└── README.md
Prerequisites

    .NET 10 SDK
    Node.js and npm compatible with the project's Vite version
    A PostgreSQL server and an accessible database
    Network access to the label-template share from the backend process
    For physical printing: a configured CUPS Linux VM and an accessible Epson printer
    The required PDF replacement font at the location expected by PdfService.cs (the project has used wwwroot/fonts/ACUMINVARIABLECONCEPT.OTF)

    Keep fonts, passwords, database backups, customer artwork, and exported production labels out of public repositories unless you have the rights and a specific reason to publish them.

Local development
1. Get the project and restore dependencies

From the solution's root directory:
dotnet restore

From the client directory:
cd printuesi.client
npm install
2. Configure the backend

Provide the PostgreSQL connection string and JWT secret using local development configuration or .NET user secrets. Also configure the label-template base path and the existing printer/CUPS settings.

Example only — adapt keys to your actual appsettings.json:
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=printuesi;Username=YOUR_USER;Password=YOUR_PASSWORD"
  },
  "Jwt": {
    "Secret": "REPLACE_WITH_A_LONG_RANDOM_SECRET"
  },
  "Labels": {
    "BasePath": "W:\\"
  },
  "RrethiPrinting": {
    "Enabled": true,
    "ProductCode": "RRETHI",
    "MultiplierPerPiece": 1
  }
}

Never commit live passwords or JWT secrets. On a different machine, W:\ will only work if that path is available to the backend process; services running under another account may not have the drive mapped.
3. Start the frontend
cd printuesi.client
npm run dev

The development frontend has been configured to use:
http://localhost:5173
4. Start the backend

From the solution root:
dotnet run --project Printuesi.Server/Printuesi.Server.csproj

Development backend URLs used in this project:
https://localhost:7179
http://localhost:5226

In printuesi.client/vite.config.js, Vite proxies /api requests to the ASP.NET backend. For example:
server: {
  port: 5173,
  strictPort: true,
  proxy: {
    "/api": {
      target: "https://localhost:7179",
      changeOrigin: true,
      secure: false // local development certificate only
    }
  }
}

If ASP.NET SPA Proxy is enabled, its URL must match the frontend protocol and port exactly:
<SpaProxyServerUrl>http://localhost:5173</SpaProxyServerUrl>

Do not mix up http://localhost:5173 and https://localhost:5173. If running Vite directly and using its /api proxy, the ASP.NET SPA Proxy is optional; avoid running two competing frontend launch mechanisms.
5. Apply EF Core migrations if needed
dotnet ef database update --project Printuesi.Server --startup-project Printuesi.Server

Run migrations only against the intended database, and back up production data first.
Configuration
Setting	Purpose
ConnectionStrings:DefaultConnection	PostgreSQL connection string.
Jwt:Secret	Key used to sign/validate JWT access tokens.
Labels:BasePath	Root of the source PDF template library.
ProductSizeMap	Maps product codes to paper categories when template metadata does not supply a size.
RrethiPrinting	Controls optional Rrethi label generation and quantity multiplier.
Cups / RemoteCups	Printing and Linux VM connection settings; confirm the exact keys in the application's configuration.
Output cleanup settings	Retention schedule and files to remove; verify the current deployed values.

The application uses four paper groups:
Paper size	Typical use
Normale	Standard product labels
Mini	Small labels
Prodhim	Production labels
Rrethi	Round/client-level labels
Printing workflows
Automatic printing

    Open Automatic Printing and upload the customer order PDF.
    The backend scans the order and prepares items with requested quantities.
    Items are grouped by paper size (Normale, Mini, Prodhim, Rrethi).
    Optionally enable Rrethi when creating the order. Its quantity is based on the configured multiplier, and it should appear as a single job item for the client.
    Open the job in Job Control.
    Change the loaded paper, then click Kam ndërruar letrën to confirm and apply the appropriate printer settings.
    For Mini and Rrethi, perform a test print before production. Test printing is also useful for the other sizes when alignment needs checking.
    Click Nis grupin for the selected group. The application should not silently advance to the next paper group.
    When a group finishes, change paper and repeat for the next group.
    Inspect generated PDFs and quantities before marking the order fully complete.

Stop limitation: the application can stop sending additional copies, but it cannot guarantee that a job already accepted by CUPS or physically entering the printer will be retracted.
<!-- GITHUB SCREENSHOT SLOT: automatic group controls -->

    📸 Screenshot placeholder — Confirm paper / Print test / Start group: docs/images/group-controls.png

Manual printing

    Open Manual Printing.
    Select the appropriate client and PDF template.
    Enter values required by that template (such as EXP, LOT, or KG).
    Select Test Preview (No Print) to produce and view a filled PDF without sending it to the printer.
    Verify the generated artwork and positioning.
    Send the selected labels to the printer when they look correct.

The preview must be fetched from an authenticated API endpoint as PDF bytes / Blob, not by opening a local file:/// path in the browser.
<!-- GITHUB SCREENSHOT SLOT: generated preview -->

    📸 Screenshot placeholder — Generated PDF Preview: docs/images/generated-pdf-preview.png

Job Control and history

For each job, the interface can display the client, paper groups, product codes, requested and printed quantities, EXP/LOT details, and generated-PDF preview controls. Jobs can have the following statuses:

Pending · In_progress · Stopped · Completed · Cancelled

Client history is available from Clients → select client → print jobs. A history row may link to Job Control using a URL such as:
/print-jobs?jobID=JOB_ID
PDF templates and placeholders

Source templates are normal PDFs stored in the shared label library. The PDF service reads a template and overlays the replacement values on the appropriate page.

Supported placeholder conventions include:
{{EXP}}   Expiration date
{{LOT}}   Lot or batch number
{{KG}}    Weight, for templates/workflows that request it

Illustrator template requirements

    Keep placeholders as real editable text when exporting to PDF; do not convert them to outlines.
    Simple fonts/encodings are more reliably extractable by iText.
    White placeholders on a white background can conceal the placeholder artwork while still allowing text extraction, but they remain embedded in the PDF.
    Export and save the actual PDF used by the backend, not just the .ai project file.
    Make EXP and LOT separate text objects with sufficient room for their replacement values.

Current PDF implementation note: the original PdfService detects PDF text, merges nearby chunks, and draws replacement text in a new content stream. This is overlay, not true redaction or deletion. Illustrator text fragmentation and approximate coordinates can cause missing placeholders or overlapping values. Test any replacement algorithm against several real customer templates before deploying it globally.

Never silently overwrite a completed job's archived PDF merely because a source template has changed. For fresh test previews and unprinted jobs, regeneration from the latest template should be explicitly supported.
<!-- GITHUB SCREENSHOT SLOT: placeholders before and after -->

    📸 Screenshot placeholder — Template versus Filled Label: docs/images/template-vs-generated.png

Typical label storage

Representative layout (confirm the current exact paths in code):
W:\
└── CLIENT_NAME\
    ├── PRODUCT_CODE CLIENT_NAME Normale.pdf
    ├── PRODUCT_CODE CLIENT_NAME Mini.pdf
    └── CLIENT_NAME Rrethi.pdf

Printuesi.Server/wwwroot/labels_output/
├── manual/
│   └── YYYY-MM-DD/
└── jobs/
    └── YYYY-MM-DD/
        └── PRINT_JOB_ID/
            └── PAPER_SIZE/
                └── PRINT_JOB_OBJECT_ID.pdf

Rrethi may use a client-specific filename instead of a product-code-based filename. The application also supports using a stored LabelTemplate.PdfPath when configured.
Users, permissions, and audit

The intended role model is:
Action	Admin	User
Dashboard, manual printing, automatic printing	✅	✅
Clients and print-job history	✅	✅
Start, test, and stop printing	✅	✅
Cancel a print job	✅	✅
Permanently delete a print job	✅	❌
Manage users, roles, and passwords	✅	❌

Authorization must be enforced by the ASP.NET API, not only by hiding buttons in React. In particular, the hard-delete endpoint should require the Admin role.

Audit requirements: record the user ID, readable user name, timestamp, and cancellation reason for cancellations; preserve an audit trail for any privileged deletion. These requirements must be verified against the current DB migrations and service/controller code before enabling production users. The history should not depend on a user account remaining undeleted.

Passwords must be hashed, never stored or returned as plain text. User management responses must not expose password hashes.
Selected API endpoints

These paths reflect endpoints established during development. Confirm exact signatures and authorization requirements in the corresponding controllers.
Method	Endpoint	Purpose
GET	/api/printjobs	List jobs
GET	/api/printjobs/{printJobID}	Get one job
POST	/api/printjobs/create-from-order	Create a job from an uploaded order PDF
POST	/api/printjobs/{id}/confirm-paper-change	Confirm paper size / apply printer setup
POST	/api/printjobs/{id}/print-test	Print test label
POST	/api/printjobs/{id}/start-group	Start selected paper group
POST	/api/printjobs/{id}/stop-printing	Request stop
DELETE	/api/printjobs/{id}	Cancel a job (soft operation)
DELETE	/api/printjobs/{id}/delete	Hard-delete eligible job (Admin only — verify)
GET	/api/printjobs/{id}/generated-pdfs	List generated PDFs
POST	/api/printjobs/{id}/generated-pdfs/{objectID}/prepare	Prepare a filled PDF
GET	/api/printjobs/{id}/generated-pdfs/{objectID}/preview	Fetch a generated PDF
POST	/api/manual-printing/test-label	Generate a manual test PDF without printing
GET	/api/clients	List clients
GET	/api/clients/{clientID}/print-jobs	Client job history
GET	/api/users	List users (Admin)
POST	/api/users	Create user (Admin)
PUT	/api/users/{userID}	Update user (Admin)
POST	/api/users/{userID}/change-password	Change user password (Admin)

Development API documentation may be available at the backend's /swagger path when Swagger is enabled.
Printer and CUPS diagnostics

Run these on the Linux VM that hosts CUPS.
# Printer/queue information
lpstat -v
lpstat -p EpsonLabel -l

# Current queued jobs
lpstat -o EpsonLabel

# Completed jobs (if CUPS history retention permits)
lpstat -W completed -o EpsonLabel

# Watch pending jobs live
watch -n 1 lpstat -o EpsonLabel

# Cancel one queued job
cancel EpsonLabel-JOB_NUMBER

# Cancel all pending jobs on the queue — use with caution
cancel -a EpsonLabel

# Show configured printer options
lpoptions -p EpsonLabel -l

Example of an existing CUPS setup pattern; replace addresses/paths with the values on your VM:
sudo lpadmin -p EpsonLabel -E \
  -v lpd://PRINTER_IP/PASSTHRU \
  -P /path/to/EPSON_CW_C6000Ae.ppd.gz

An application-generated PDF may be submitted with lp -d EpsonLabel ... and paper/quality/cut options. The exact lp -o values must match the installed PPD/driver.
<!-- GITHUB SCREENSHOT SLOT: cups -->

    📸 Screenshot placeholder — CUPS queue / Epson configuration: docs/images/cups-queue.png

Troubleshooting
401 Unauthorized when previewing a label

Check browser DevTools → Network → failing request. If the response contains www-authenticate: Bearer error="invalid_token", the JWT may have expired. Log out and log back in. A PDF preview request must include the same bearer token as other authenticated API requests.
Browser refuses to load file:///...

A web page served from http://localhost:5173 cannot directly open arbitrary Windows filesystem paths. Serve the generated PDF through an authorized backend endpoint and preview the returned application/pdf Blob.
Old artwork appears after updating a template

    Verify the source PDF (not only the Illustrator project) was saved.
    Log the exact resolved template path, last-write time, and file size.
    Check whether an already-generated job PDF is being reused.
    Try a new job/fresh test preview rather than a completed job archive.
    Check whether the backend account resolves the same mapped/shared path that you edited.

EXP / LOT missing or overlapping

    Ensure the placeholders exist as extractable PDF text and use the exact braces {{EXP}} / {{LOT}}.
    Make placeholders separate text objects and avoid converting to curves/outlines.
    Examine PdfService logs for extracted text chunks and replacement coordinates.
    White placeholders must be paired with black generated text (useWhiteText: false) where the label background is white.
    The overlay algorithm does not remove the source placeholder; do not assume a new PDF-processing patch is compatible without testing multiple existing templates.

SPA Proxy cannot connect to Vite

Match .csproj's SPA URL to the actual Vite URL, including protocol:
Frontend: http://localhost:5173
ASP.NET:  https://localhost:7179

Alternatively, run Vite and ASP.NET independently and let Vite proxy /api to the backend.
CUPS accepts a job but nothing prints

Check lpstat, queue state, installed Epson PPD, printer connectivity, and the exact options sent in the lp command. CUPS accepting a job does not guarantee that the physical printer finished it.
Swagger/OpenAPI build problems

Use an internally compatible combination of Swashbuckle.AspNetCore and Microsoft.OpenApi packages. Recent OpenAPI major versions use APIs different from the old Microsoft.OpenApi.Models / OpenApiReference examples. Review the resolved transitive package tree before pinning or upgrading.
dotnet list Printuesi.Server/Printuesi.Server.csproj package --include-transitive
Generated files and retention

Generated PDFs support quality control and traceability. Do not commit labels_output into Git.

Retention was discussed as a longer-term policy (e.g., one year for completed jobs, shorter periods for some cancelled/manual-test output), but the deployed OutputCleanupService must be checked before relying on those periods. A development log has shown a 30-day cleanup cutoff, which may not match the desired archive policy.

Suggested approach:

    Keep completed job PDFs long enough for operational and compliance needs.
    Keep an audit trail even when a user cancels a job.
    Hard-delete only where policy permits, and record privileged deletion events.
    Treat temporary remote CUPS transfer files differently from the authoritative local archive.
    Back up the DB and archive according to company policy.

Screenshots to add

Put screenshots in docs/images/. Replace each visible 📸 placeholder above with its corresponding Markdown image link.
Filename	Screenshot to capture
dashboard.png	Dashboard, summary cards, and quick actions
automatic-printing.png	Automatic Printing order upload
manual-printing.png	Manual Printing form and test controls
print-jobs.png	Jobs Control table and selected job
clients.png	Client cards and job history
users.png	Users list and editor (hide sensitive information)
group-controls.png	Paper change confirmation and print buttons
generated-pdf-preview.png	Filled PDF preview with dummy EXP/LOT values
template-vs-generated.png	Before/after placeholder example using non-sensitive artwork
cups-queue.png	CUPS status/queue (hide IPs and user identities if needed)

Example replacement:
![Job Control screen](docs/images/print-jobs.png)

Use sample/dummy customers and avoid revealing passwords, JWTs, private network details, or production-only artwork in screenshots.
Development roadmap

Items being improved or planned:

    Paper-specific Driver Settings screen: save/apply presets and iteratively tune vertical, horizontal, and cut/feed offsets.
    More robust Illustrator/PDF placeholder matching and accurate replacement positioning.
    Fresh-preview regeneration controls without modifying completed-job archives.
    Verified administrator-only permanent deletion and durable cancellation audit records.
    Confirm and configure generated-file retention periods.
    Full end-to-end tests for job cancellation, CUPS failures, printer disconnection, and reconnect/retry scenarios.
    Production deployment and backup/recovery documentation.

Security and licensing

    Store credentials in environment variables, secret storage, or a managed secret provider; never commit .env, secrets, or live appsettings credentials.
    Verify JWT expiry, role claims, and backend authorization on sensitive actions.
    Restrict access to customer templates, generated PDFs, and database backups.
    Review dependency security advisories before upgrades, then run regression tests.
    Review iText licensing and any font licensing before distributing or publishing the full application.
    No repository license is specified in this README. Add a LICENSE file only after the project owner selects the appropriate terms.

Printuesi — label preparation, printer automation, and print-job traceability.
