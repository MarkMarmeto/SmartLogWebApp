# User Personas

Personas for SmartLog School Information Management System. Referenced in user stories to ensure features are designed with specific users in mind.

**Last Updated:** 2026-02-03

---

## 1. Tech-Savvy Tony (Super Admin)

**Role:** IT Department Head
**Technical Proficiency:** Expert
**Primary Goal:** Deploy and maintain SmartLog efficiently across the school with minimal manual intervention

### Background
Tony manages the school's IT infrastructure and is responsible for all technology systems. He has 10+ years of experience in educational IT and handles everything from network setup to software deployment. He's the go-to person when anything technical breaks.

### Needs & Motivations
- Streamlined deployment process (Docker makes his life easier)
- Comprehensive audit logs for troubleshooting and compliance
- Clear documentation for system maintenance
- Ability to configure the system without touching code
- Reliable backups and recovery procedures

### Pain Points
- Manual setup and configuration for each system component
- Lack of visibility into what users are doing when problems occur
- Systems that require constant hand-holding
- Poor documentation that forces trial-and-error
- Vendors who don't understand school IT constraints (no cloud, LAN-only)

### Typical Tasks
- Initial system deployment and configuration
- User account provisioning for new staff
- Troubleshooting login and access issues
- Database backups and maintenance
- Security updates and system monitoring

### Quote
> "Just give me a Docker Compose file and good documentation. I'll handle the rest."

---

## 2. Admin Amy (School Administrator)

**Role:** Administrative Assistant
**Technical Proficiency:** Intermediate
**Primary Goal:** Efficiently manage student and staff records with minimal errors

### Background
Amy works in the school's administrative office and handles student enrollment, records management, and parent communications. She's comfortable with computers and learns new software through training, but prefers clear interfaces over complex options. She's been at the school for 5 years.

### Needs & Motivations
- Quick access to student information during phone calls with parents
- Bulk operations for start-of-year enrollment
- Clear feedback when actions succeed or fail
- Easy QR code printing for student IDs
- Reports for school administration

### Pain Points
- Repetitive data entry for large student batches
- Unclear error messages that don't explain what went wrong
- Having to contact IT for simple tasks
- Paper-based systems that are slow and error-prone
- Parents calling about attendance when records aren't accessible

### Typical Tasks
- Adding new students and updating records
- Generating and printing QR codes for student IDs
- Creating user accounts for new teachers and staff
- Running attendance reports for administration
- Updating parent contact information

### Quote
> "I need to find this student's information before the parent on the phone loses patience."

---

## 3. Teacher Tina (Classroom Teacher)

**Role:** Classroom Teacher
**Technical Proficiency:** Intermediate
**Primary Goal:** Quickly check class attendance and access student information when needed

### Background
Tina teaches Grade 5 and manages a class of 35 students. She uses technology daily for lesson planning and communication but isn't interested in learning complex systems. She just wants tools that work without getting in the way of teaching.

### Needs & Motivations
- At-a-glance view of who's present/absent in her class
- Quick lookup of student details and parent contacts
- Mobile-friendly interface for checking attendance on her tablet
- Notifications when a student arrives late
- Simple, uncluttered interface

### Pain Points
- Wasting class time on administrative tasks
- Not knowing a student is absent until roll call
- Having to go to the office to get parent contact information
- Systems that require too many clicks for simple tasks
- Forgetting passwords for rarely-used systems

### Typical Tasks
- Checking class attendance at the start of the day
- Looking up parent contact when a student is sick
- Viewing student information for parent-teacher meetings
- Checking if a specific student has arrived at school

### Quote
> "I have 35 students and 45 minutes. Don't make me click through five screens to see who's absent."

---

## 4. Guard Gary (Security Personnel)

**Role:** School Gate Security Guard
**Technical Proficiency:** Novice
**Primary Goal:** Quickly verify student identity and record entry/exit without delays

### Background
Gary works at the school's main gate, scanning student QR codes as they enter and exit. He's not comfortable with technology and needs the simplest possible interface. He handles hundreds of students during peak times (morning arrival, afternoon dismissal) and can't afford delays.

### Needs & Motivations
- Instant feedback when scanning (green = OK, red = problem)
- Minimal interaction required - just point and scan
- Clear visual/audio confirmation
- Works even when network is slow or down
- Easy way to handle exceptions (forgotten ID, new student)

### Pain Points
- Technology that slows down the entry queue
- Confusing interfaces with too many buttons
- Systems that freeze or crash during peak times
- Having to remember passwords or procedures
- Dealing with angry parents when lines are long

### Typical Tasks
- Scanning student QR codes at entry
- Scanning student QR codes at exit
- Reporting when a QR code doesn't work
- Alerting admin when an unrecognized person tries to enter

### Quote
> "Green means go, red means stop. That's all I need to know."

---

## 5. Staff Sarah (Office Clerk)

**Role:** Office Clerk
**Technical Proficiency:** Novice
**Primary Goal:** Look up student information to answer inquiries and handle paperwork

### Background
Sarah works in the school office handling general inquiries, distributing materials, and managing paperwork. She occasionally needs to look up student information but doesn't manage records directly. She's cautious with technology and prefers read-only access.

### Needs & Motivations
- Read-only access to student directory
- Simple search by name or student ID
- Ability to verify if a student is enrolled
- Clear indication of what she can and cannot do
- No risk of accidentally changing data

### Pain Points
- Accidentally modifying records when trying to view them
- Too many features and options she doesn't need
- Having to ask Admin Amy for information she should be able to look up
- Unclear what information she's allowed to share with visitors

### Typical Tasks
- Looking up student information for visitors
- Verifying student enrollment status
- Finding which class a student belongs to
- Looking up a student's parent/guardian contact

### Quote
> "I just need to look things up. I don't want to break anything."

---

## Persona Summary

| Persona | Role | Proficiency | Primary Access |
|---------|------|-------------|----------------|
| Tech-Savvy Tony | Super Admin | Expert | Full system access, configuration |
| Admin Amy | Administrator | Intermediate | User, student, faculty management |
| Teacher Tina | Teacher | Intermediate | View attendance, view student info |
| Guard Gary | Security | Novice | QR scanning (via WPF app) |
| Staff Sarah | Office Clerk | Novice | Read-only student lookup |

---

## Changelog

| Date | Changes |
|------|---------|
| 2026-02-03 | Initial personas created |
