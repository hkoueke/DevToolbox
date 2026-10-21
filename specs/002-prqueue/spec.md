# Feature Specification: prqueue — the pull requests waiting on you, across the whole collection (read-only)

**Feature Branch**: `002-prqueue`

**Created**: 2026-08-25

**Status**: Draft

**Input**: User description: "prqueue"

## Clarifications

### Session 2026-08-25

- Q: What makes this worth a tool, when Azure DevOps already lists pull requests? → A: **The collection-wide
  view.** The web interface answers "which pull requests await me *in this project*". A developer working
  across several projects has no single place that answers "which await me at all", and must visit each
  project in turn and remember which ones they visited. The tool's reason to exist is removing that sweep,
  which is also why project scope is a first-class concern rather than a detail. Recorded as FR-004.
- Q: Does it order by creation date, like the web interface? → A: **No — by how long it has been waiting.**
  A queue is read from the top, and the useful top is the review that has been waiting longest, not the one
  opened most recently. Recorded as FR-008.
- Q: Does "waiting on you" cover the developer's own pull requests as well as reviews awaited from them? →
  A: **Reviews only, for this version.** The queue answers "what must I do", not "what of mine is stuck".
  Covering authored pull requests means reading why each cannot complete, which is a second body of work
  answering a different question; it is expected to arrive later as its own section rather than being
  dropped. Because a tool named for a queue of pull requests would otherwise be read as covering both, it
  must state which of the two it reports on. Recorded as FR-012 and FR-012a.
- Q: A reviewer in Azure DevOps may be a group. How far should the tool go to notice that the developer is
  awaited through one? → A: **As far as the server allows, and it declares the rest.** Group memberships
  are resolved where they can be, and those pull requests appear marked as awaited through a group rather
  than personally. A group whose membership cannot be established is uninspected scope, not evidence of
  non-membership — so it is named, and it prevents the queue being called complete. Restricting the tool to
  direct assignments was rejected: on a team that assigns reviews by group, that would produce a short,
  confident, wrong list every morning. Recorded as FR-006 and FR-006a.

### Session 2026-08-25 — raised during planning

- Q: The Assumptions say waiting starts "when the developer became able to act". Planning found that the
  server does not report that moment — a pull request carries its creation date, not the date a reviewer was
  added or the date it came back to them. What should the wait be measured from? → A: **From creation, and
  the screen says so.** Measuring the real moment would mean reading every pull request's iteration and
  thread history, one call each, to answer a question that ordering a list only needs approximately right.
  The column is therefore labelled as age since the pull request was opened, which is honest and needs no
  extra reads. A pull request reopened after changes will look older than it has been awaited — accepted,
  and stated. Recorded as FR-008a; the original assumption is corrected.
- Q: A reviewer can decline a review outright. Is a declined reviewer still awaited? → A: **No.** A decline
  is an answer, not silence. The tool treats it exactly as a cast vote: the developer is no longer awaited,
  and the pull request leaves their queue. This was missing from the spec and would otherwise have surfaced
  as a permanently stuck entry nobody could clear. Recorded as FR-007a.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See everything awaiting my review at once (Priority: P1)

A developer starts their day, opens the toolbox and picks `prqueue`. Without choosing a project, they get
one list of every pull request across the collection that is waiting for their review, longest wait first,
each showing its repository, its title, who opened it, where it is going, and how long it has been sitting
there. They work down the list.

**Why this priority**: This is the tool. Everything else is a refinement of this list. A developer who works
across four projects currently answers this question by visiting four pages and holding the union in their
head, which is exactly the kind of sweep a terminal does better.

**Independent Test**: With the developer named as reviewer on pull requests in at least two different
projects, confirm both appear in one list without a project ever being chosen, and that a pull request they
are not a reviewer on does not.

**Acceptance Scenarios**:

1. **Given** pull requests awaiting the developer in several projects, **When** the queue is displayed,
   **Then** all of them appear in one list, ordered by how long each has been waiting, longest first.
2. **Given** a pull request the developer has already voted on, **When** the queue is displayed, **Then** it
   is not presented as awaiting their review, since it is not.
3. **Given** a project that could not be read, **When** the queue is displayed, **Then** that project is
   named as not inspected, and the queue is never presented as complete.
4. **Given** nothing is awaiting the developer, **When** the queue is displayed, **Then** the tool says so
   plainly, and states what it inspected in order to say it.

---

### User Story 2 - Know who I am looking at (Priority: P1)

Before reading anything, the developer sees which identity the queue belongs to — the account their Windows
session resolves to on this server. If that identity cannot be established, the tool says so and stops,
rather than showing a list filtered by nothing.

**Why this priority**: A queue is meaningless without knowing whose it is, and an empty list is the most
dangerous possible output: it reads as "nothing to do" whether it means that or means the filter failed.
This shares P1 because the first story is unsafe without it.

**Independent Test**: Run the tool and confirm the identity it announces matches the account the server sees;
then run it where the identity cannot be resolved and confirm it refuses rather than listing.

**Acceptance Scenarios**:

1. **Given** a working session, **When** the tool starts, **Then** it announces the identity whose queue is
   being shown, before reading anything.
2. **Given** an identity that cannot be established, **When** the tool starts, **Then** it explains that and
   stops, and MUST NOT display a list.

---

### User Story 3 - Decide whether to review now without leaving the terminal (Priority: P2)

The developer picks one entry and sees enough to triage it: its description, its source and target branches,
how large it is, who else is reviewing and how they have voted, and whether anything is blocking it. They
decide to take it now or leave it, and the tool tells them where to go and review it.

**Why this priority**: The list tells you what is waiting; this tells you which to take first. Valuable, but
the list alone already replaces the sweep.

**Independent Test**: Open an entry with several reviewers and confirm the votes shown match the server, and
that the location to go and review it is stated.

**Acceptance Scenarios**:

1. **Given** an entry in the queue, **When** it is opened, **Then** its description, branches, size, other
   reviewers and their votes, and anything blocking it are shown.
2. **Given** that entry is open, **When** the developer has read it, **Then** the tool states where the
   review is performed and offers no means of voting, commenting, or completing from within the tool.

---

### User Story 4 - Notice what has gone stale (Priority: P3)

The developer asks which of the reviews awaited from them have been waiting beyond a threshold they set, so
that nothing sits forgotten for a fortnight.

**Why this priority**: It falls out of the ordering the queue already computes and costs little, but it is a
periodic hygiene question rather than a daily one.

**Independent Test**: With a threshold of seven days and a pull request waiting nine, confirm it is listed
and one waiting two days is not.

**Acceptance Scenarios**:

1. **Given** a staleness threshold, **When** the stale list is displayed, **Then** only entries waiting
   longer than it are shown, with their wait stated.
2. **Given** any project that could not be read, **When** the stale list is displayed, **Then** the tool
   states that the list cannot be treated as exhaustive, and why.

---

### Edge Cases

- **A reviewer that is a group, not a person.** Azure DevOps lets a team or group be named as reviewer. A
  developer belonging to that group is genuinely awaited, but no pull request names them individually. Such
  entries are included and marked as awaited through the group; a group whose membership cannot be
  established is declared rather than assumed away. A queue that quietly omits everything assigned to one's
  team is worse than no queue.
- **A project that cannot be read** is named as not inspected, and its existence prevents the tool from
  presenting the queue as complete. Otherwise "nothing is waiting" would be indistinguishable from "I could
  not look".
- **A draft pull request** is not awaiting review: its author has not asked for one. It is excluded from the
  review queue and marked as draft wherever it does appear.
- **A pull request the developer authored** does not appear, even when they are also named as a reviewer on
  it: nobody is awaiting their review of their own change. This follows from FR-012 and is stated because
  the opposite is a reasonable thing to expect.
- **A pull request with no reviewers at all** is not in anyone's queue, and no queue should imply otherwise.
- **A pull request whose target branch has been deleted**, or which is abandoned or already completed, is not
  awaiting anything and is excluded, not shown as blocked.
- **A pull request set to complete automatically**, held only by an unsatisfied policy, is waiting on the
  policy rather than on the developer, and is described that way.
- **A collection with many projects**: reads are bounded and parallelism is bounded, progress is reported,
  and the run stays cancellable. Cancelling leaves a described state — which projects were inspected.
- **A very long title, branch, or repository name** cannot break the display or push a column off screen.
- **A non-interactive session** is told so and the tool exits cleanly rather than waiting on a prompt nobody
  can answer.

## Requirements *(mandatory)*

### Functional Requirements

**Identity**

- **FR-001**: The tool MUST authenticate with the developer's existing Windows session and MUST NOT prompt
  for a password, a token, or a user name.
- **FR-002**: The tool MUST establish which identity on the server the session resolves to, and MUST
  announce that identity before reading anything.
- **FR-003**: When the identity cannot be established, the tool MUST explain that and stop. It MUST NOT
  display a queue, because an unfiltered or empty list would be read as "nothing awaits you".

**The queue**

- **FR-004**: The tool MUST answer across the whole collection, without requiring the developer to choose a
  project first.
- **FR-005**: The tool MUST list every pull request awaiting the developer's review, showing for each its
  project, its repository, its title, its author, its target branch, and how long it has been waiting.
- **FR-006**: The tool MUST include pull requests where the developer is awaited through a group or team
  rather than by name, wherever their membership of that group can be established, and MUST mark such an
  entry as awaited through a group rather than personally.
- **FR-006a**: A group whose membership cannot be established MUST be treated as uninspected scope under
  FR-013 — named, and preventing the queue from being called complete. The tool MUST NOT read an
  unresolvable membership as evidence that the developer is not a member.
- **FR-007**: A pull request the developer has already voted on MUST NOT be presented as awaiting their
  review.
- **FR-007a**: A reviewer who has declined the review MUST NOT be presented as awaiting it either. A decline
  is an answer, not silence.
- **FR-008**: The queue MUST be ordered by how long each entry has been waiting, longest first.
- **FR-008a**: The wait MUST be measured from the pull request's creation, and the display MUST say so.
  The server does not report when a reviewer was added or when a pull request returned to them, and
  reconstructing it would cost one further read per entry to refine an ordering that only needs to be
  approximately right.
- **FR-009**: Draft pull requests MUST be excluded from the review queue, and marked as drafts wherever they
  appear.
- **FR-010**: Abandoned, completed, and otherwise closed pull requests MUST be excluded.
- **FR-011**: The developer MUST be able to narrow the collection to a subset of projects, and that choice
  MUST be remembered between runs so a large collection is swept once rather than every morning.

**Scope of the queue**

- **FR-012**: "Waiting on you" means a review is awaited from the developer. Pull requests the developer
  authored are out of scope for this version: the tool MUST NOT list them and MUST NOT report on whether
  they can complete.
- **FR-012a**: The tool MUST state, where the queue is displayed, that it shows reviews awaited from the
  developer and not the state of their own pull requests. A developer whose own pull request is blocked must
  not be able to read this queue's silence as reassurance.

**Honesty about what was read**

- **FR-013**: Every project or repository that could not be read, and every group whose membership could not
  be established, MUST be named, and its presence MUST prevent the tool from presenting the queue as
  complete.
- **FR-014**: A statement that nothing is waiting MUST be accompanied by what was inspected in order to make
  it. The tool MUST NOT state an absence it did not establish.
- **FR-015**: When any part of the queue is incomplete, the tool MUST label it as partial and state what is
  missing.

**Triage**

- **FR-016**: The developer MUST be able to open one entry and see its description, its source and target
  branches, its size, the other reviewers and their votes, and anything blocking it.
- **FR-017**: The tool MUST state where a review is actually performed, and MUST offer no means of voting,
  commenting, completing, abandoning, or otherwise acting on a pull request.
- **FR-018**: The developer MUST be able to list entries waiting longer than a threshold they choose.

**Interface**

- **FR-019**: Every dialogue the developer reads MUST be in French — prompts, menu entries, column headings,
  legends, summaries and failure explanations alike.
- **FR-020**: Every state and vote MUST be conveyed by text or a symbol in addition to colour, and the
  display MUST degrade correctly on a terminal without colour or Unicode.
- **FR-021**: Every list MUST offer a way back that is reachable without scrolling and distinguishable at a
  glance from the items being chosen.
- **FR-022**: Reading MUST report progress, MUST remain cancellable, and MUST leave a described state behind
  when cancelled — which projects were inspected, which were not.
- **FR-023**: When the queue is too wide for the terminal, the tool MUST fall back to a layout that shows
  everything rather than truncating, and MUST NOT drop an entry.

**Read-only guarantee**

- **FR-024**: The tool MUST NOT create, update, or delete any Azure DevOps resource, and MUST NOT vote,
  comment, complete, abandon, or change any pull request. Every request it issues MUST be a retrieval; no
  request may change server state.
- **FR-025**: The tool MUST NOT display, log, or persist the content of a pull request's changes. It shows
  what is waiting and why, not what the change contains.

### Key Entities

- **Developer identity**: the account the Windows session resolves to on the server. Everything the queue
  shows is relative to it, which is why it is announced rather than assumed.
- **Pull request**: a proposed change in a repository, with an author, a source and target branch, a state,
  and a creation time.
- **Review assignment**: the link between a pull request and someone expected to review it — directly or
  through a group — together with their vote, or its absence. Which of the two routes brought an entry into
  the queue is part of the entry, because "your team is awaited" and "you are awaited" are different facts.
- **Blocking reason**: why a pull request cannot complete — an outstanding review, an unsatisfied policy, a
  conflict. Here it serves triage: a review that cannot merge even once given is one to take later.
- **Wait**: how long an entry has been awaiting the developer. It is what the queue is ordered by, and it is
  derived, never stored.
- **Inspection scope**: which projects and repositories were actually read for a given answer. Every
  statement about absence is relative to it, and it is always shown.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer sees everything awaiting their review across every project in one list, without
  choosing a project, in under 15 seconds.
- **SC-002**: Every pull request genuinely awaiting the developer appears in a queue the tool presents as
  complete — none is ever silently missing.
- **SC-003**: The queue is never presented as complete when any project could not be read; in 100% of such
  cases the result is qualified and the uninspected projects are named.
- **SC-004**: For a collection of up to 50 projects, the queue is complete within 30 seconds, with progress
  shown throughout.
- **SC-005**: The identity whose queue is displayed is stated on every run, and no list is ever shown
  without it.
- **SC-006**: No pull request is ever modified by the tool, verified by the absence of any state-changing
  request in its behaviour.
- **SC-007**: The developer can triage an entry — decide to take it or leave it — without opening a browser.
- **SC-008**: The developer can leave any step through a visible way back, without resorting to Ctrl+C.

## Assumptions

- **`varcompare` is the reference.** Windows session sign-in, the configured collection, French interface,
  Spectre rendering, resumable steps, bounded parallel reads, progress reporting and the "undetermined is
  not absent" discipline are reused rather than reinvented.
- **The collection is the unit, not the project.** This is the one thing the web interface does not do, and
  it is the tool's reason to exist. The project filter in FR-011 exists to bound cost, never to become the
  normal way of using it.
- **Cost may grow with the number of projects.** Whether the server can answer the question once for the
  whole collection, or must be asked project by project, is a property of the server version rather than of
  this feature. SC-004 is stated for 50 projects on the assumption that the per-project path is the worst
  case; if the collection is far larger, FR-011 is what keeps the tool usable.
- **"Waiting" is measured from creation.** ~~It starts when the developer became able to act — when they were
  added as a reviewer, or when the pull request last changed in a way that returned it to them.~~
  **SUPERSEDED** during planning: the server reports neither moment, and reconstructing them costs one extra
  read per entry to refine an ordering that only needs to be approximately right. See FR-008a — the wait is
  the pull request's age, and the display says so.
- **A vote already cast means the developer is not awaited**, even if the pull request is still open. If the
  author pushes again and the vote is reset, it is awaited once more.
- **Authored pull requests are deferred, not rejected.** FR-012 bounds this version to reviews awaited from
  the developer; "what of mine is stuck" is expected to arrive later as a second section, reading why each
  pull request cannot complete. FR-012a exists so that the gap is stated on screen in the meantime, rather
  than being inferred from an empty screen.
- **Group membership is resolved through whatever the server exposes.** How much it exposes is a property of
  the server and of the developer's permissions, not of this feature. FR-006a is what keeps a partial answer
  honest instead of silently short.
- **Read-only means no state change, not one particular request shape.** A retrieval expressed as a query is
  still a retrieval; what the tool must never do is change anything.
- **The temptation here is approval.** A queue of reviews sitting one keystroke from a vote is the obvious
  next feature and the one that would end the read-only guarantee. FR-017 and FR-024 exist to make that a
  deliberate decision rather than a drift.
- **The server is an on-premises Azure DevOps Server** reached over integrated Windows authentication, as
  established for `varcompare`.
