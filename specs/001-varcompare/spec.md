# Feature Specification: varcompare — Azure DevOps Variable Group Comparison (read-only)

**Feature Branch**: `001-varcompare`

**Created**: 2026-08-20

**Status**: Draft

**Input**: User description: "write a tool function named varcompare that reads variable groups of a repository on azure devops, asks the user which variables groups he wants to compare, reads the variables and values of each variable groups, and tells if a variable is preent in each group or not, if a variable has a value or not, if the variable is linked to a keyvault. color coded comparison, and the user can choose a variable to write to a variable group along with its value. See my repo https://github.com/hkoueke/AzureRepoGovernance/tree/main/src for details, no password will be used, the apps works by using the users session."

## Clarifications

### Session 2026-08-20

- ~~Q: When the tool needs to reach Azure DevOps, which sign-in already present on the developer's machine
  should it use? → A: Chained fallback (Windows/Entra → Azure CLI → Visual Studio → browser).~~
  **SUPERSEDED** — see the last entry in this session. That answer assumed Azure DevOps Services in the
  cloud; the target is an on-premises Azure DevOps Server.
- Q: How should the developer tell the tool which project's variable groups to work on? → A: Auto-detect,
  then ask — when the toolbox runs inside a cloned Azure DevOps repository, read the collection and project
  from that repository's remote and confirm them; otherwise ask. A manual override is always available.
- Q: What should the tool do when the developer picks more variable groups than can be shown legibly side by
  side? → A: Warn, then change layout — past the legible threshold the tool abandons the side-by-side grid and
  lists each variable with its per-group states stacked underneath, with no cap on the number of groups.
- Q: Should the actual values of non-secret variables be shown on screen, or only whether a value exists? →
  A: Values on demand — the comparison shows state only; selecting a variable opens a detail view showing its
  real value in each group.
- Q: When a developer resumes an interrupted run, what should the tool restore, and does that change if the
  toolbox was closed in between? → A: Full resume in-session — while the toolbox is open, continuing from a
  failed step reuses everything already retrieved; across a restart only the collection, project, and chosen
  groups are remembered, and the groups' contents are re-read.
- Q: Should the tool be able to write a variable into a variable group? → A: **No — the tool is read-only.**
  Comparison and inspection only; the developer makes any change directly in Azure DevOps. FR-019 through
  FR-027 were removed and FR-018 now states the read-only guarantee.
- Q: How does the tool learn the server base URL — is it edited into a settings file before first use? → A:
  **The tool asks on first run and remembers the answer.** The value shipped in `appsettings.json` is a
  *hint* shown as the default, not a value the developer is expected to edit. On first start the tool prompts
  for the server base URL and collection, validates them, and saves them to the per-user settings file; on
  every later start it uses what was saved. This is a server address, not a credential — it does not
  introduce a credential prompt and SC-007 is unaffected.
- Q: What exactly is "the user's session", and which Azure DevOps is being targeted? → A: **Windows
  integrated authentication against an on-premises Azure DevOps Server.** Established from the reference
  implementation at `hkoueke/AzureRepoGovernance`, which authenticates with the machine's integrated Windows
  credentials (Negotiate/NTLM/Kerberos) against a server such as
  `https://devops.entreprise.local/DefaultCollection`. There is no token, no browser sign-in, and no
  credential prompt of any kind. This supersedes the first entry in this session.

### Session 2026-08-21

- Q: In which language does the tool address the developer? → A: **French.** Every dialogue the developer
  reads — prompts, menu entries, legends, summaries, failure explanations and startup diagnostics — is
  written in French. This is a presentation decision only: identifiers, log messages, code comments and
  specification artefacts are unaffected, and no behaviour changes with it. Recorded as FR-012a.
- Q: What may the developer type when the first-run prompt asks where the server is? → A: **The short host
  name is enough.** In-house a server is named `azure`, sometimes written `azure/`, and the scheme is
  understood rather than typed. Refusing that spelling would demand a form the developer writes nowhere
  else. The `https://` scheme is therefore supplied when — and only when — none was given; an explicit
  scheme is always honoured, never replaced. FR-031a still holds: what is understood is `https`, plain HTTP
  remains refused without the explicit opt-in, and the prompt still declines the opt-in. The same reading
  applies to the options validator, so one address cannot be accepted at the prompt and rejected at startup.

### Session 2026-08-25

- Q: FR-016b abandons the side-by-side grid "when the selected groups exceed what can be displayed legibly".
  How is that threshold established? → A: **By measuring what will actually be written, not by reserving a
  worst case.** The threshold reserved a fixed width per column, wide enough for the longest possible state
  label with both qualifiers, and it ignored the group name that titles the column. A comparison whose cells
  all read *définie* was therefore stacked on a terminal that had ample room for its columns. The
  presentation now measures the widest cell and header it is about to render, per column, and hands those
  widths to the layout selector. The grid stays the default whenever it genuinely fits, the stacked layout
  remains the fallback, and FR-016e is unaffected — a column too narrow for its group name now counts as not
  fitting, which it always should have. Recorded as FR-016f.
- Q: How does the developer leave a step they did not mean to reach? → A: **Every choice list carries its own
  marked way back.** The group selection was a multiple-choice list requiring at least one checked entry, so
  the only exit was Ctrl+C; the project and menu lists did offer *Retour*, but as an entry indistinguishable
  from a project or a tool, at the bottom of a page the developer had to scroll through. Navigation entries
  now carry an ASCII arrow prefix that no name can be mistaken for, they stay at the end of the list — never
  first, where Enter would trigger them unintentionally — and the lists wrap around, so a single Up keystroke
  reaches them. Recorded as FR-004a.

### Session 2026-08-26

- Q: Resuming an interrupted run failed immediately at retrieval with *Aucun groupe n'a encore été
  sélectionné*, leaving no recourse but to start over. What is the rule that was broken? → A: **A resume may
  declare a step completed only if the resume itself re-deposits that step's result.** The checkpoint records
  the names of completed steps, and the resume marked every one of them completed except retrieval and
  comparison. But the run context lives only in memory, and a skipped step deposits nothing: listing and
  selection were declared done while `AvailableGroups` and `SelectedGroups` were never set, so retrieval
  found no selection and failed. Only *identify project* is genuinely re-deposited by the resume, so it alone
  is carried over; listing and selection are replayed, and the remembered selection is reapplied without
  asking the developer again. FR-039b is unchanged in intent — this makes it true in practice. Recorded as
  FR-039e.
- Q: A slow server ended the run outright instead of offering the four recovery options. Why? → A: **A
  timeout was being reported as a cancellation.** The API reader mapped every `OperationCanceledException` to
  *cancelled*, and the step runner ends a run without prompting whenever a step fails as cancelled — which is
  right for a developer pressing Ctrl+C and wrong for a server that was merely slow. A timeout is now told
  apart from a cancellation by whether the caller's token was actually signalled, and reported as a service
  failure, so it reaches the recovery prompt like any other. Recorded as FR-038a.
- Q: FR-036 and FR-037 require the tool to say that it is retrying and that it is waiting out throttling.
  Nothing said either. Why? → A: **The retries happen beneath the HTTP client, where no tool can observe
  them.** The retrieval progress port carried `Retrying` and `Throttled` members that nothing ever called,
  because the resilience pipeline lives in the shared infrastructure and knows nothing of a tool's groups.
  The announcement now belongs to the shared shell through a retry observer port, so every tool inherits it
  without registering anything, and the two unreachable members are gone from the tool's port. No group name
  is named: a burst of parallel reads would make one misleading. Recorded as FR-036a.
- Q: Should a settings combination DevToolbox accepts be one the resilience pipeline accepts? → A: **Yes, and
  it is now checked at startup.** The standard handler requires at least one retry and a circuit-breaker
  sampling window of at least twice the attempt timeout, and it checks neither until the typed client is
  first created — mid-run, as an exception. It also races the `HttpClient` timeout against its own total
  budget when the two are equal, which the shipped settings were. The client timeout is now required to
  exceed the pipeline budget (shipped as 120 s against 30 s), and both pipeline constraints are restated on
  the options type so `ValidateOnStart` catches them. Recorded as FR-038b.
- Q: What reaches the developer when the pipeline itself gives up? → A: **A failure, never an exception.**
  An exhausted time budget raises `TimeoutRejectedException` and an open circuit `BrokenCircuitException`;
  neither derives from `HttpRequestException`, so both escaped the API reader, traversed every layer and
  terminated the process. Both are now caught where they arise and translated like any other outcome. FR-033
  and FR-038 already implied this; nothing but the reader changes. Recorded as FR-038c.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Compare variable groups side by side (Priority: P1)

A developer opens the toolbox, picks the `varcompare` tool, and confirms the Azure DevOps collection and
project — detected from the repository they are working in when there is one, chosen from a list otherwise —
and is shown the list of variable groups available to them. They select two or more groups (for example
`app-dev`, `app-uat`, `app-prod`) and immediately see a single comparison table: one row per distinct
variable name, one column per selected group, with each cell colour-coded to show whether that variable
exists in that group and whether it carries a value.

**Why this priority**: This is the entire reason the tool exists. Configuration drift between environment
variable groups is invisible today — a developer must open each group in the browser, one at a time, and
compare by eye. A single colour-coded table delivers the whole value of the feature on its own.

**Independent Test**: Point the tool at a project containing at least two variable groups with deliberately
divergent contents (one variable present in both, one missing from one group, one present but empty). Verify
the table renders every distinct variable name exactly once and that each cell's state matches the real
contents of the group.

**Acceptance Scenarios**:

1. **Given** a developer with read access to a project containing three variable groups, **When** they launch
   `varcompare` and select two of those groups, **Then** a comparison table is displayed containing the union
   of all variable names across both groups, sorted predictably, with one column per selected group.
2. **Given** a variable that exists in group A but not in group B, **When** the comparison is displayed,
   **Then** the cell for group A shows a "present" state and the cell for group B shows a distinct "missing"
   state, differentiated by both colour and a text or symbol marker.
3. **Given** a variable that exists in a group with an empty value, **When** the comparison is displayed,
   **Then** that cell is shown in a state distinct from both "present with a value" and "missing".
4. **Given** the developer selects fewer than two groups, **When** they attempt to continue, **Then** the tool
   explains that at least two groups are required and returns them to the selection step without losing their
   earlier choices.
5. **Given** a project containing no variable groups the developer may read, **When** they select that
   project, **Then** the tool states that no comparable groups were found and offers to pick another project
   or return to the menu.
6. **Given** the toolbox is running inside a folder cloned from an Azure DevOps repository, **When** the
   developer starts `varcompare`, **Then** the collection and project derived from that repository are shown
   for confirmation, and the developer can accept them or choose a different project.
7. **Given** the toolbox is running outside any Azure DevOps working folder, **When** the developer starts
   `varcompare`, **Then** they are offered the projects they can access in the configured collection.

---

### User Story 2 - Understand the state and origin of each variable (Priority: P2)

Having seen the table, the developer needs to interpret it: which values actually differ, which variables are
secrets whose values cannot be revealed, which groups are backed by a key vault (so their contents are managed
elsewhere), and which variables are marked read-only. They can toggle to show only rows that differ, and can
drill into a single variable to see its per-group detail.

**Why this priority**: Without this, the table answers "is it there?" but not "can I trust it?". A developer
who does not know a group is key-vault-backed will misread an unreadable value as a missing one. It builds
directly on P1 and is worthless without it.

**Independent Test**: Compare one ordinary group against one key-vault-backed group containing a secret and a
read-only variable. Verify the origin, secret, and read-only indicators are correct and that no secret value
is ever displayed.

**Acceptance Scenarios**:

1. **Given** a selected group that is backed by a key vault, **When** the comparison is displayed, **Then**
   the group's column is clearly labelled as key-vault-backed and every variable in it is marked as sourced
   from the key vault.
2. **Given** a variable whose value is held as a secret, **When** the comparison is displayed, **Then** the
   tool indicates that a value exists but is not retrievable, and never displays, logs, or persists a secret
   value.
3. **Given** a variable present in all selected groups with values that are not identical, **When** the
   developer enables the "differences only" filter, **Then** that row remains visible while rows identical
   across all selected groups are hidden, and the count of hidden rows is stated.
4. **Given** a variable marked read-only in a group, **When** the developer views the comparison, **Then**
   that variable is marked read-only for that group.
5. **Given** a comparison result of any size, **When** it is displayed, **Then** a legend explains every
   colour and symbol used, and every state is distinguishable without relying on colour alone.
6. **Given** a comparison is on screen, **When** the developer opens the detail view for a chosen variable,
   **Then** its readable value in each selected group is shown; and **When** no variable has been opened,
   **Then** no variable value appears anywhere on screen.

---

### User Story 3 - Recover an interrupted run (Priority: P3)

A comparison across five groups fails on the fourth group because of a network drop or an authentication
failure. The developer is offered the choice to retry the failed step, continue from that step, restart the
whole tool function, or abort — and the work already completed is not thrown away.

**Why this priority**: Required by the project constitution for every tool function, and it materially improves
the experience on flaky corporate networks. It is a cross-cutting behaviour rather than a standalone user
outcome, so it ships after the core value.

**Independent Test**: Induce a failure on one group retrieval mid-run. Verify all four recovery options are
offered, that "continue" does not re-fetch already-retrieved groups within the session, that a resume after
closing and reopening the toolbox restores only the selection and re-reads the groups, and that aborting
ends the run cleanly.

**Acceptance Scenarios**:

1. **Given** a multi-step run fails at a named step, **When** the failure is reported, **Then** the developer
   is offered at least: retry the failed step, continue from the failed step, restart the entire function, and
   abort.
2. **Given** the developer chooses to continue, **When** the run resumes, **Then** steps already completed are
   not repeated and the run proceeds from the failed step.
3. **Given** the developer aborts, **When** the run ends, **Then** the tool states plainly which groups had
   been read and which had not, and returns to the menu.
4. **Given** a run was interrupted and the toolbox was then closed, **When** the developer launches
   `varcompare` again, **Then** the previous collection, project, and group selection are offered for
   resumption or discard, the groups' contents are re-read rather than restored from memory of the earlier
   run, and no variable value was retained in order to make that possible.
5. **Given** a transient connectivity or throttling failure, **When** it occurs, **Then** the tool retries
   automatically before surfacing a failure, and tells the developer it is retrying rather than appearing
   frozen.

---

### Edge Cases

- **Not authenticated to the server**: the machine's Windows session is not trusted by the Azure DevOps
  Server, or the developer has no account there. The tool reports that the server rejected the integrated
  Windows credentials and names the server it tried — it never prompts for a username or password.
- **Server unreachable**: the configured server is not resolvable or not responding (off the corporate
  network, VPN down). Reported as such, distinctly from a permission failure.
- **Server not configured**: no server base URL or collection has been saved yet — the ordinary first-run
  state. The tool asks for them, showing the shipped hint as the default, validates the answer, and saves it
  for next time. It never fails obscurely, and it never asks for a credential. If a saved value later becomes
  invalid, the tool says which setting is wrong and offers to correct it rather than refusing to start.
- **Undetectable or misleading working folder**: the folder has no remote, several remotes, a remote pointing
  somewhere other than the configured Azure DevOps Server, or a repository the developer cannot access. The
  tool says which case applies and asks instead of guessing.
- **Detected project is not the one wanted**: the developer is in one repository but needs another project's
  groups — overriding must not require restarting the tool.
- **No access**: the developer can reach the collection but has no permission to list variable groups in the
  chosen project — reported as a permission issue, not as an empty result.
- **Partial access**: some selected groups are readable and others are not — the readable ones are still
  compared, and the unreadable columns are marked as such rather than shown as empty.
- **Case-insensitive names**: two groups contain `API_KEY` and `Api_Key`. The tool treats them as the same
  variable, shows the differing casing, and warns that the platform does not distinguish them.
- **Duplicate group names** across projects, or two groups with the same name selected: each column is
  identified unambiguously.
- **Single group in project**: comparison requires two; the tool says so instead of showing a useless table.
- **Many groups selected**: beyond the number that fits legibly, the tool warns and switches to the stacked
  layout. It never caps the selection and never silently truncates data.
- **Large groups**: several hundred variables across the union — the table remains navigable and states the
  total row count.
- **Empty group**: a selected group with zero variables shows as an entirely "missing" column, not as an error.
- **Key-vault-backed group whose vault is unreachable**: the group is shown with its variable names and an
  explicit "state unavailable" marker rather than being reported as empty.
- **Value edge content**: values containing newlines, very long values, or characters that could disrupt the
  display are rendered safely wherever values are shown, and shortened with an explicit indicator.
- **Narrow or non-interactive terminal**: a narrow terminal falls back to the stacked layout rather than
  clipping columns; if the session cannot accept interactive input, the tool says so and exits cleanly rather
  than hanging.
- **Group changed while being compared**: another person edits a group after it was read — the comparison
  reflects the moment of reading, and the developer can refresh to pick up changes.
- **Throttling**: the service rate-limits the developer; the tool waits as instructed and informs the developer
  rather than hammering the service.

## Requirements *(mandatory)*

### Functional Requirements

**Discovery and selection**

- **FR-001**: The tool MUST be available as a named function `varcompare` within the toolbox, reachable from
  the menu, and MUST allow the developer to leave it and return to the menu at any prompt.
- **FR-002**: The tool MUST identify the Azure DevOps Server collection and project to work against.
  Specifically:
  - **FR-002a**: When the toolbox is running inside a working folder cloned from a repository on the
    configured Azure DevOps Server, the tool MUST derive the collection and project from that repository's
    remote and present them for confirmation rather than applying them silently.
  - **FR-002b**: When no such repository is detected — or its remote does not point at the configured server
    — the tool MUST offer the projects the developer can access in the configured collection, rather than
    requiring exact typing.
  - **FR-002c**: The developer MUST be able to override the detected collection and project at any point and
    switch to a different project without leaving the tool.
  - **FR-002d**: When the working folder has several remotes, or a remote the tool cannot interpret, the tool
    MUST say so and fall back to asking rather than guessing.
- **FR-003**: The tool MUST list the variable groups the developer is permitted to read in the chosen
  project, showing each group's name and whether it is key-vault-backed.
- **FR-004**: The tool MUST let the developer select two or more groups to compare, and MUST reject a selection
  of fewer than two with an explanation.
  - **FR-004a**: Every list the developer chooses from MUST offer a way back that is reachable without
    scrolling and distinguishable at a glance from the items being chosen. Choosing it MUST leave the step
    without the explanation owed to an incomplete selection, since nothing was being selected.
- **FR-005**: The tool MUST support filtering or searching the group list by name when the project contains
  many groups.

**Comparison and display**

- **FR-006**: The tool MUST retrieve the variables of every selected group and build a comparison over the
  union of all variable names found.
- **FR-007**: The tool MUST treat variable names case-insensitively when matching across groups, while
  displaying the name as it is actually stored in each group.
- **FR-008**: For every variable and every selected group, the tool MUST report exactly one of these states:
  *absent from this group*, *present with a value*, *present but empty*, *present with a secret value that
  cannot be read*, or *present but its state could not be determined*.
- **FR-009**: The tool MUST indicate, per group, whether that group is key-vault-backed, and MUST mark every
  variable belonging to such a group as key-vault-sourced.
- **FR-010**: The tool MUST indicate variables that are marked read-only in a given group.
- **FR-011**: The tool MUST indicate when a variable is present in every selected group but its readable values
  are not identical.
- **FR-012**: The tool MUST colour-code every state and MUST additionally convey each state through text or a
  symbol, so the comparison remains unambiguous without colour.
  - **FR-012a**: Every dialogue the developer reads MUST be in French — prompts, menu entries, legends,
    summaries, failure explanations and startup diagnostics alike. Identifiers, log messages and step names
    are not dialogue and are unaffected.
- **FR-013**: The tool MUST display a legend defining every colour and symbol used in the comparison.
- **FR-014**: The tool MUST offer a "differences only" view that hides variables identical across all selected
  groups and states how many rows were hidden.
- **FR-015**: The tool MUST allow the developer to inspect a single variable to see its per-group detail.
  Specifically:
  - **FR-015a**: The comparison itself MUST show state only. It MUST NOT render variable values, whether in
    the side-by-side or the stacked layout.
  - **FR-015b**: The detail view MUST be opened by an explicit action on a chosen variable, and MUST show
    that variable's readable value in each selected group alongside its state.
  - **FR-015c**: The detail view MUST NOT reveal a secret value or the content of a key-vault-backed
    variable; it MUST state that a value exists and cannot be read.
  - **FR-015d**: Values shown in the detail view MUST NOT be written to logs or to recovery state.
- **FR-016**: The tool MUST state the total number of distinct variables compared and MUST never silently omit
  or truncate a variable from the comparison. Specifically:
  - **FR-016a**: There MUST be no fixed upper limit on the number of groups that may be compared.
  - **FR-016b**: When the selected groups exceed what can be displayed legibly side by side, the tool MUST
    warn the developer before rendering, and MUST then present the comparison as a stacked layout: each
    variable listed once, with its state in every selected group shown beneath it.
  - **FR-016c**: The stacked layout MUST convey exactly the same states, colours, symbols, legend, and
    "differences only" filtering as the side-by-side grid.
  - **FR-016d**: The developer MUST be able to switch between the side-by-side and stacked layouts manually,
    whatever the number of selected groups.
  - **FR-016e**: Neither layout may drop a group or a variable; if a layout cannot show everything, the tool
    MUST say so rather than render a partial result.
  - **FR-016f**: "Displayed legibly" MUST be established by measuring the width the comparison will actually
    occupy — the widest cell and the group name titling each column — and not by reserving a worst case per
    column. The side-by-side grid is the default whenever it fits.
- **FR-017**: Wherever a value is displayed — that is, the detail view — the tool MUST render long or
  multi-line values in a way that cannot disrupt the display, indicating explicitly when a displayed value
  has been shortened.

**Read-only guarantee**

- **FR-018**: The tool MUST be strictly read-only with respect to Azure DevOps. Specifically:
  - **FR-018a**: The tool MUST NOT create, update, rename, or delete any variable, variable group, or any
    other Azure DevOps resource. Changes are made by the developer directly in Azure DevOps.
  - **FR-018b**: The tool MUST NOT issue any state-changing request to Azure DevOps. Only retrieval
    operations are permitted.
  - **FR-018c**: The tool MUST NOT present any affordance suggesting a value can be edited from within it;
    where a difference is found, it MAY tell the developer where to go and change it.
  - **FR-018d**: The tool MUST require only read access. It MUST NOT request or rely on permission to manage
    variable groups.

  *(FR-019 through FR-027 described the former write capability and were removed on 2026-08-20 when the tool
  became read-only. Numbering is preserved so existing references stay valid.)*

**Security and privacy**

- **FR-028**: The tool MUST authenticate using the developer's existing Windows session. Specifically:
  - **FR-028a**: The tool MUST use integrated Windows authentication (Negotiate/Kerberos/NTLM) with the
    credentials already held by the running process.
  - **FR-028b**: The tool MUST NOT prompt for, accept, read, or store a username, a password, a personal
    access token, or any other credential. No credential entry surface may exist anywhere in the tool.
  - **FR-028c**: The tool MUST state which server and collection it is talking to before performing any
    read, so the developer can recognise an unexpected target.
  - **FR-028d**: The tool MUST NOT follow automatic redirects, so credentials are never presented to a host
    other than the one configured.
- **FR-029**: The tool MUST operate strictly within the permissions of the signed-in developer and MUST NOT
  attempt to elevate or work around a denial.
- **FR-030**: The tool MUST never display, log, persist, or checkpoint a secret value, the contents of a
  key-vault-backed variable, or any credential material.
- **FR-031**: The tool MUST validate its configured server base URL and the collection and project
  identifiers it is given, and MUST contact only that configured server. Specifically:
  - **FR-031a**: The configured server base URL MUST be HTTPS. A plain-HTTP server MUST be refused unless an
    explicit, clearly-named opt-in is set. A base URL given without a scheme — the short host name by which
    an internal server is known — MUST be read as HTTPS; an explicit scheme MUST be honoured rather than
    replaced. The prompt and the options validator MUST apply the same reading, so that no address is
    accepted by one and refused by the other.
  - **FR-031b**: Certificate validation MUST NOT be disabled by any configuration option.
  - **FR-031c**: Collection, project, and group identifiers MUST be validated and escaped before being placed
    in a URL.
- **FR-032**: When the server rejects the developer's Windows credentials, the tool MUST say so plainly,
  name the server and collection it tried, and return to the menu — never prompting for credentials.

**Reliability and recovery**

- **FR-033**: The tool MUST execute as an ordered sequence of named steps (identify project → list groups →
  select groups → retrieve each group's variables → build comparison) whose progress is visible to the
  developer.
- **FR-034**: On failure of any step, the tool MUST offer at minimum: retry the step, continue from the failed
  step, restart the whole function, and abort.
- **FR-035**: Continuing after a failure MUST NOT repeat work already completed successfully within the same
  toolbox session — groups already retrieved MUST be reused rather than retrieved again.
- **FR-036**: The tool MUST retry transient failures automatically with increasing delays before surfacing an
  error, and MUST tell the developer that a retry is in progress.
  - **FR-036a**: The retry announcement MUST come from the shared shell rather than from a tool, because the
    retries happen inside the shared HTTP client where no tool can observe them. A tool MUST NOT have to
    register anything to inherit it. The announcement MUST NOT name a resource: reads run in parallel, so
    naming one would mislead.
- **FR-037**: The tool MUST respect service throttling instructions rather than retrying immediately.
- **FR-038**: The tool MUST stop calling a repeatedly failing service and report it as unavailable, rather than
  retrying indefinitely.
  - **FR-038a**: A request that exceeds its time budget MUST be reported as a service failure and MUST NOT be
    reported as a cancellation. Only the developer's own cancellation ends a run without offering the
    recovery choices of FR-034.
  - **FR-038b**: A settings combination the tool accepts MUST be one the resilience pipeline accepts, and
    MUST be rejected at startup rather than at the first request. The per-request client timeout MUST exceed
    the pipeline's total request budget, so the two deadlines cannot race.
  - **FR-038c**: When the resilience pipeline itself gives up — an exhausted time budget, an open circuit —
    the outcome MUST reach the developer as a described failure. No such condition may surface as an
    unhandled exception.
- **FR-039**: An interrupted run MUST be offered for resumption or discard on the next launch. Specifically:
  - **FR-039a**: The state that survives closing the toolbox MUST be limited to the collection, the project,
    the chosen groups, and which steps had completed. Variable values — secret or not — MUST NOT survive the
    session.
  - **FR-039b**: Resuming after a restart MUST re-read the chosen groups rather than presenting remembered
    contents, so the developer never acts on a stale picture.
  - **FR-039c**: A remembered interrupted run MUST expire after 7 days and MUST be discardable by the
    developer at any time.
  - **FR-039d**: If a remembered group no longer exists or is no longer readable on resume, the tool MUST say
    so and let the developer continue with the remaining groups or change the selection.
  - **FR-039e**: A resume MUST declare a step completed only if the resume itself re-deposits that step's
    result into the run context. Every other completed step MUST be replayed. A remembered selection MUST be
    reapplied without asking the developer again, and a resume MUST reach the comparison without the
    developer having to restart the function.
- **FR-040**: Aborting or cancelling MUST end the run in a described state, stating which groups had been
  read and which had not.

**Observability**

- **FR-041**: Every run MUST be logged with a single identifier that ties together the whole run, including
  the tool identifier and version recorded at run start, step transitions, each group retrieved, retries,
  and the final outcome with its duration.
- **FR-042**: Every group retrieval MUST be logged with the collection, project, group name and id, and the
  outcome — never a variable value.
- **FR-043**: Log output MUST NOT disturb the on-screen comparison.

### Key Entities

- **Server**: the Azure DevOps Server instance the tool talks to; identified by its configured HTTPS base URL.
- **Collection**: a collection on that server (for example `DefaultCollection`); contains projects.
- **Project**: a project within the collection; owns the library of variable groups being compared.
- **Variable Group**: a named collection of variables belonging to a project. Has a name, a description, an
  origin (ordinary or key-vault-backed), the time it was last modified, and a set of variables.
- **Variable**: a named entry within a group. Has a name (matched case-insensitively across groups), an
  optional readable value, a secret indicator, a read-only indicator, and — when its group is key-vault-backed
  — a key-vault origin.
- **Comparison**: the result of comparing two or more groups. One row per distinct variable name, one cell per
  group, each cell carrying exactly one state; plus summary counts (total variables, variables differing,
  variables missing somewhere).
- **Cell State**: absent / present with value / present but empty / present but secret / state undetermined,
  optionally qualified as key-vault-sourced or read-only.
- **Run**: one execution of `varcompare`, identified for logging and recovery, consisting of ordered named steps
  with recorded completion state.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer who knows their project can go from opening the tool to a rendered comparison of two
  variable groups in under 60 seconds and no more than five interactions.
- **SC-002**: The comparison accounts for 100% of the variables present in the selected groups — verified by
  comparing the tool's row count against the sum of distinct names in the source groups, with zero silent
  omissions.
- **SC-003**: Every distinct state is identifiable without colour, confirmed by reviewing the output with
  colour disabled and correctly classifying 100% of cells.
- **SC-004**: Zero secret values, key-vault-backed values, or credential material appear in the screen output,
  the log files, or any stored recovery state — verified by inspecting all three after a run that includes
  secrets.
- **SC-005**: A comparison of five groups totalling 500 distinct variables is displayed within 5 seconds of the
  data being retrieved, and remains navigable.
- **SC-006**: Across every run, 100% of requests issued to Azure DevOps are retrieval requests — verified by
  inspecting the recorded request log; zero state-changing requests are ever issued.
- **SC-007**: Zero credential prompts are reachable anywhere in the tool — verified by inspecting every
  interactive prompt the tool can present.
- **SC-008**: After an induced mid-run failure in a live session, a developer choosing "continue" completes the
  run without any already-retrieved group being retrieved again; after a resume in a new session, the stored
  recovery state contains zero variable values.
- **SC-009**: Identifying which environment is missing a given variable takes under 1 minute with the tool,
  against roughly 5 minutes navigating the web portal group by group.
- **SC-010**: 100% of runs produce a log trace that allows an observer to reconstruct which groups were read
  and what the outcome was.
- **SC-011**: When more groups are selected than fit side by side, 100% of variables and 100% of selected
  groups remain visible in the stacked layout — verified by counting rows and per-group entries against the
  source groups — and the developer is warned before the layout changes.
- **SC-012**: Zero variable values appear in the comparison display; a value is visible only after the
  developer explicitly opens that variable's detail view — verified by inspecting the rendered comparison for
  a group whose values are known.

## Assumptions

- **The target is an on-premises Azure DevOps Server**, reached at a configured HTTPS base URL with a
  collection segment (for example `https://devops.entreprise.local/DefaultCollection`), api-version 7.1.
  This is taken from the reference implementation `hkoueke/AzureRepoGovernance`. Azure DevOps Services in the
  cloud is **not** targeted; supporting it later would mean adding a token-based authentication path.
- **Authentication is integrated Windows authentication** using the credentials of the running session. No
  password, no personal access token, no browser sign-in, and no credential prompt exists anywhere in the
  tool.
- **Variable groups are project-scoped, not repository-scoped.** The request said "variable groups of a
  repository", but variable groups live in a project's library and are not attached to a repository. The tool
  works at collection + project level, and bridges the gap by deriving that project from the repository the
  developer is working in when one is detectable (FR-002a).
- **Permissions are the developer's own.** The tool shows exactly what the signed-in developer is allowed to
  see; there is no service account and no elevation.
- **Secret values cannot be read back.** Azure DevOps does not return secret values, so the tool can report
  that a secret exists but never its content. The same applies to key-vault-backed groups, whose values live
  in the vault.
- **Key-vault linkage is a property of the group, not of an individual variable.** A variable is reported as
  key-vault-sourced when its group is key-vault-backed.
- **The tool is read-only.** Adding, editing, renaming, or deleting variables and groups is out of scope and
  actively prevented; the developer makes changes directly in Azure DevOps.
- **Comparison requires at least two groups**; there is no useful single-group mode.
- **Comparison results are not exported** (no file, clipboard, or report output) in this feature.
- **Recovery state that survives closing the toolbox is limited to selections and step progress**, expires
  after 7 days, and contains no variable values. The 7-day window is a chosen default — adjust it if your
  environment expects otherwise.
- **The tool runs inside the DevToolbox interactive console session**, under the same menu, tab, logging,
  resilience, and resume/restart behaviour the project constitution mandates for every tool function.
