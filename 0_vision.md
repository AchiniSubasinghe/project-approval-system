## Project Scenario

In academic institutions, the process of assigning supervisors to final-year projects is often manual and prone to partiality. To ensure a fair and interest-driven allocation, the University requires a system where students can propose innovative ideas and supervisors can select projects that align with their specific expertise.

This coursework involves developing the "Blind-Match" PAS. The platform will allow students to submit project proposals categorized by research areas (e.g., Artificial Intelligence, Web Development, Cybersecurity). Supervisors will browse these proposals without knowing the identity of the students. Once a supervisor confirms interest in a project, the system will "reveal" the identities of both parties, enabling formal collaboration. This system empowers the faculty to maintain high academic standards and ensures students are paired with the most qualified mentors for their specific topics.

---

## User Roles and Functional Requirements

### Student (Individual or Group)

**Responsibilities:** Project ideation, proposal submission, and tracking approval status.

**Functional Requirements:**

- Secure Login: Individual or Group Lead account.
- Project Submission: Create a new project proposal (Title, Abstract, Technical Stack, and Research Area).
- Manage proposal details (View, Edit, or Withdraw before it is matched).
- Status Tracking:
  - View the status of the project (e.g., "Pending," "Under Review," "Matched").
  - The Reveal: Once a supervisor confirms the match, the student must be able to see the Supervisor's name and contact details.

### Supervisor (Faculty Member)

**Responsibilities:** Defining expertise, reviewing anonymous proposals, and selecting projects for supervision.

**Functional Requirements:**

- Secure Login: Faculty account.
- Expertise Management: Select preferred research areas/tags (e.g., "Machine Learning," "Cloud Computing").
- Blind Review Dashboard: Browse a list of available projects filtered by preferred research areas.
- Anonymity Constraint: View project details (Title, Abstract, Tech Stack) without seeing the student's name or ID.
- Matching Logic: Express interest in a project.
- Confirm Match: Once confirmed, the system must trigger the "Identity Reveal," showing the student's details to the supervisor and vice versa.

### Module Leader (Coordinator/Admin)

**Responsibilities:** Oversight of the allocation process, system configuration, and user management.

**Functional Requirements:**

- Secure Login: Administrative access.
- Research Area Management: Define and manage the list of valid research areas/tags available in the system.
- Allocation Oversight: View a comprehensive dashboard of all matches (Who is paired with whom).
- Manually intervene or reassign projects if necessary.
- User Management: Create and manage accounts for Supervisors and Students.

### System Administrator (Web Master)

**Responsibilities:** Infrastructure management, database versioning, and security configurations.

**Functional Requirements:**

- Environment Configuration: Setup of the ASP.NET Core environment and SQL Server connectivity.
- Database Versioning: Implementation of Entity Framework Core Migrations to manage schema changes.
- Security Setup: Implementation of Role-Based Access Control (RBAC) to ensure Students cannot access Supervisor dashboards and vice versa.
