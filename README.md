# DiaCompanion Lite

### Offline-ready research platform for AI-assisted diabetic retinopathy screening

**A hospital research data-collection build focused on privacy,
reproducible grading, and practical deployment.**

[![React](https://img.shields.io/badge/Frontend-React%20%2B%20TypeScript-149eca?logo=react&logoColor=white)](https://react.dev/)
[![.NET](https://img.shields.io/badge/Backend-.NET%208-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Docker](https://img.shields.io/badge/Deployment-Docker%20Compose-2496ED?logo=docker&logoColor=white)](https://www.docker.com/)
[![Research](https://img.shields.io/badge/Use-Research%20prototype-2f855a)](#scope-and-clinical-disclaimer)
:::

> **Research software --- not a medical device.** DiaCompanion Lite is
> intended for controlled research and software evaluation. AI outputs
> must not be used as a standalone diagnosis or as a substitute for
> qualified clinical judgement.

------------------------------------------------------------------------

## Overview

DiaCompanion Lite is a research-focused continuation of the DiaCompanion
capstone project. It packages the core workflow for collecting and
reviewing data for AI-assisted diabetic retinopathy (DR) screening,
while keeping patient identity separate from research records.

The system is designed for environments where deployment must be
predictable, data handling must be privacy-conscious, and the
application may need to run without a continuous Internet connection.

### Key goals

-   **Privacy by design:** use a research code in the application rather
    than storing direct patient identity in the research dataset.
-   **Blind reference grading:** preserve the clinician's initial
    assessment before AI results are revealed, with independent grading
    and adjudication steps.
-   **Human review:** use the application as a research workflow and
    data-collection tool, not as an autonomous diagnostic system.
-   **Offline deployment:** provide a Windows installer for controlled
    environments without requiring developers' runtimes to be installed
    separately.
-   **Portable deployment:** also support a Docker Compose workflow for
    development, testing, and server deployment.
-   **Analysis-ready exports:** export de-identified research data with
    a data dictionary to support downstream statistical analysis.

## Project status

-   **Project:** independent research build developed after the original
    capstone.
-   **Current use:** pilot/testing on a test server before formal data
    collection.
-   **Deployment paths:** Docker Compose and an offline Windows
    installer.
-   **Automated verification:** 72 end-to-end API tests reported for
    core workflows, including authentication/roles, anonymization,
    grading rules, and exports.
-   **Research export:** de-identified CSV with a 59-column data
    dictionary.

> The figures above describe the current project build and its test
> coverage; they are not evidence of clinical effectiveness or
> regulatory approval.

## Features

### Research data collection

-   Record examinations using a research identifier.
-   Keep the identity-to-research-code mapping outside the application,
    under the institution's control.
-   Collect structured grading and review data.
-   Export de-identified records for analysis, accompanied by a data
    dictionary.

### Blind and independent grading

The research workflow is designed to reduce information leakage from AI
predictions into the reference labels:

1.  **Initial clinician grading:** the doctor records the assessment
    before AI results are run or revealed.
2.  **Independent grading:** a second grader reviews the case
    independently.
3.  **Adjudication:** a designated reviewer resolves disagreements
    according to the study protocol.
4.  **Export:** eligible, de-identified records are prepared for
    research analysis.

Exact permissions and transitions depend on the configuration and
release deployed. Follow the study protocol and the application's
current role rules.

### AI-assisted analysis

The broader DiaCompanion AI pipeline includes modules for:

-   Diabetic retinopathy grading.
-   Retinal lesion analysis/segmentation.
-   Retinal vessel analysis and fractal-related measurements.

Model availability and output fields depend on the model assets and
configuration bundled with the release. Results are research outputs and
require human interpretation. Do not infer a model's clinical readiness
from the fact that it runs successfully.

### Deployment options

-   **Docker Compose:** reproducible multi-service deployment for
    development, testing, and server environments.
-   **Offline Windows installer:** designed to bundle the application
    runtime, database components, and AI dependencies for a controlled
    x64 Windows environment.
-   **Local operation:** supports workflows that do not depend on a
    cloud-hosted patient database. Any external messaging or integration
    feature may have separate connectivity requirements.

## Architecture

At a high level, the research build separates the user interface,
application/business logic, persistent research data, and AI inference
runtime.

``` mermaid
flowchart TD
    U[Research user / clinician] --> FE[React + TypeScript web UI]
    FE --> API[.NET 8 Web API]
    API --> DB[(SQL Server)]
    API --> AI[Python AI inference service]
    AI --> MODELS[DR grading / lesion / vessel analysis models]
    API --> FILES[Protected image and result storage]
    API --> EXPORT[De-identified CSV + data dictionary]
```

### Main components

  -----------------------------------------------------------------------
  Component                           Responsibility
  ----------------------------------- -----------------------------------
  Web frontend                        Research screens, case review,
                                      grading and export workflow

  Backend API                         Authentication/authorization,
                                      workflow rules, validation, data
                                      access and export

  SQL Server                          Structured research records and
                                      application data

  Python AI service                   Loads supported models and performs
                                      inference

  Protected file storage              Fundus images and generated AI
                                      artifacts, subject to configured
                                      access controls

  Installer / Compose                 Repeatable deployment of the
                                      required services
  -----------------------------------------------------------------------

The diagram is conceptual. Actual service names, ports, volumes, and
configuration keys must be taken from the Compose file or installer
version in the checked-out repository.

## Technology stack

-   **Frontend:** React, TypeScript, Vite
-   **Backend:** ASP.NET Core / .NET 8
-   **Database:** Microsoft SQL Server / SQL Server Express, depending
    on deployment
-   **AI service:** Python, FastAPI, PyTorch, TensorFlow/Keras, OpenCV
    and supporting computer-vision libraries
-   **Deployment:** Docker Compose; Inno Setup-based offline Windows
    installer
-   **Testing:** end-to-end API tests for core authorization,
    anonymization, grading and export workflows

## Getting started

### Prerequisites

For Docker deployment:

-   Git
-   Docker Desktop on Windows, or Docker Engine + Docker Compose on
    Linux
-   Enough RAM and disk space for SQL Server, Python dependencies and
    the model files

The AI runtime can be resource-intensive. A machine with 16 GB RAM can
run the stack, but memory usage depends on which services and models are
enabled. Avoid running several heavy AI stacks at once.

For the offline Windows installer, use the release package and
installation guide provided with that release. The installer is intended
to bundle the runtime dependencies; you should not need to install
Node.js, Python, or .NET manually for the packaged deployment.

### 1. Clone the repository

``` bash
git clone https://github.com/anlv6000/DiaCompanion-Lite-Docker.git
cd DiaCompanion-Lite-Docker
```

### 2. Locate the Compose file

The repository may contain Compose files in a subdirectory. Locate the
file before starting services.

**PowerShell**

``` powershell
Get-ChildItem -Recurse -File -Include "compose.yml","compose.yaml","docker-compose.yml","docker-compose.yaml" |
  Select-Object -ExpandProperty FullName
```

Then change into the directory containing the intended Compose file.

### 3. Configure the environment

Review the repository's `.env.example`, Compose file, and deployment
documentation. Create the required local environment file(s) and set
values appropriate for your machine.

-   Do not commit `.env` files or real credentials.
-   Do not expose SQL Server or the AI inference service directly to the
    public Internet.
-   Check volume mounts before running the system so research data is
    written to the intended location.
-   Use synthetic data during setup and demonstration.

Do not copy environment variable names from a different version of the
project; use the names actually referenced by the checked-out release.

### 4. Build and start

From the directory containing the intended Compose file:

``` bash
docker compose config
docker compose up -d --build
docker compose ps
```

`docker compose config` validates the Compose configuration before
startup. If the repository provides a specific Compose override or
deployment command, follow that version's instructions instead.

### 5. Inspect logs and access the application

``` bash
docker compose logs --tail 100
docker compose ps
```

Use the **host-mapped frontend port** listed in the Compose file or
project documentation to open the web application in a browser. Do not
assume a port number without checking the current configuration.

### 6. Stop the stack

``` bash
docker compose down
```

This normally removes containers and the Compose network but keeps named
volumes. **Do not run `docker compose down -v` unless you intentionally
want to delete the named volumes and their data.** Back up research data
according to the institution's policy before changing or removing
deployment resources.

## Offline Windows deployment

The project also has an Inno Setup-based Windows installer intended for
controlled offline deployment.

Typical deployment characteristics of the packaged build include:

-   x64 Windows target.
-   Bundled application and AI runtime dependencies.
-   Local SQL Server Express deployment.
-   Local web access from a browser.
-   Application data and logs stored separately from installed program
    files.

Exact installer filenames, service names, installation paths, ports, and
upgrade behavior vary by release. Read the installation notes shipped
with the exact installer; do not assume paths from an older build apply
to a newer one.

For a hospital or research workstation:

1.  Obtain the installer from a trusted release/build location.
2.  Verify the checksum if one is published.
3.  Use an approved Windows account and follow the installation guide.
4.  Confirm the web application and required services are healthy.
5.  Use test records first and validate exports before formal data
    collection.
6.  Configure backup, access control, patching, and operational
    ownership before real research use.

## Testing and quality checks

The project reports **72 end-to-end API tests** covering core workflows
such as:

-   Sign-in and role-based access.
-   Research anonymization.
-   Grading rules and workflow constraints.
-   Data exports.

Run the test command defined by the checked-out repository and inspect
the actual test report. Test counts can change between versions; a
passing API suite does not, by itself, validate AI model performance,
usability, security, or clinical safety.

Before a study begins, also verify:

-   Database migrations and schema version.
-   Role permissions for each research role.
-   Initial clinician grading is recorded before AI results become
    available.
-   Independent grading and adjudication behave according to the
    protocol.
-   Exported data contains no direct identifiers or unexpected file
    paths.
-   Image access and generated artifacts are protected.
-   Backup and restore procedures work.
-   Model versions and configuration are recorded for reproducibility.

## Privacy and security

Privacy is a core design goal, but deployment configuration and
institutional procedures still matter.

-   Use research IDs in the application; keep any re-identification key
    outside the application under institutional control.
-   Do not upload patient data, fundus images, database backups, logs,
    or exports to a public repository.
-   Use synthetic data for screenshots, demo videos, and public demos.
-   Keep secrets in environment files or a secret manager, never in
    source control.
-   Restrict access to the host, database, image storage and AI service.
-   Do not expose internal database or inference ports through a tunnel.
-   Back up research data securely and test restoration.
-   Apply applicable institutional ethics approval, consent, retention
    and access policies before collecting real data.

### Public demo warning

Do not expose a research instance to the public Internet with real
patient data. If you need to demonstrate the UI remotely, use synthetic
records and restrict access. A temporary tunnel is not a production
deployment or a substitute for authentication, HTTPS policy, monitoring,
or institutional approval.

## Limitations

-   The application is a research data-collection and evaluation tool,
    not a certified diagnostic device.
-   AI model performance depends on the dataset, image quality, model
    version and validation protocol.
-   A working inference endpoint is not proof of clinical validity or
    generalizability.
-   CPU inference can be slow and model services may consume several
    gigabytes of RAM.
-   Offline operation applies to the packaged local workflow; external
    integrations may still require connectivity.
-   Features, model assets, ports, and installer behavior can differ
    between releases.

## Repository layout

The exact tree may change by release. The main areas are expected to
include equivalents of:

``` text
DiaCompanion-Lite-Docker/
├── Backend/             # .NET API and business logic
├── Frontend/            # React + TypeScript web application
├── ai_service/          # Python inference service and model adapters
├── installer/           # Windows offline installer scripts/assets
├── docs/                # Deployment, user and research documentation
├── docker-compose.yml   # Multi-service deployment (location may vary)
└── README.md
```

Check the actual repository tree before relying on this illustrative
layout.

## Roadmap

Potential next steps for a research-ready release:

-   Document exact versions and checksums for packaged models and
    dependencies.
-   Publish a reproducible deployment guide for each supported
    environment.
-   Improve test reports and release-level verification.
-   Record model version, configuration and inference status alongside
    each research result.
-   Validate the complete export against a documented data dictionary.
-   Conduct protocol-driven evaluation on approved datasets before
    making clinical performance claims.

## Contributing

Contributions that improve reproducibility, privacy, accessibility,
testability, and deployment documentation are welcome. Avoid submitting
patient data, private keys, credentials, proprietary model weights, or
institution-identifying material in issues and pull requests.

## Citation and acknowledgement

If you use this project in academic work, cite the repository and the
specific release/commit you evaluated. Describe the actual version,
dataset, protocol, and results used in your study. Do not present
project functionality as evidence of clinical benefit without
appropriate evaluation.

## License

Add the repository's actual license here. If no `LICENSE` file is
present, the code should not be assumed to be open source or freely
reusable.
