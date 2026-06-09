# Company Sample Data

Generated from: Data\company-database-documentation.json

This export is intended for documentation. Sensitive employee fields are not included. Passwords and password hashes are restricted and never exported.

## Departments

| DepartmentId | DepartmentName |
| --- | --- |
| 1 | Engineering |
| 2 | Human Resources |
| 3 | Finance |
| 4 | Sales |
| 5 | Marketing |

## Blocks

| BlockId | BlockCode | BlockName | Campus | Building | Floor | Wing | LocationDescription | Purpose | IsActive |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | BLK-A | Atria Block | Main Campus | Atria Tower | Floor 1 | North Wing | Main Campus, Atria Tower, Floor 1, North Wing | Engineering collaboration and platform delivery | 1 |
| 2 | BLK-HR | People Block | Main Campus | Atria Tower | Floor 2 | East Wing | Main Campus, Atria Tower, Floor 2, East Wing | Human Resources and employee support | 1 |
| 3 | BLK-FN | Ledger Block | Main Campus | Cedar Tower | Floor 3 | South Wing | Main Campus, Cedar Tower, Floor 3, South Wing | Finance operations and planning | 1 |
| 4 | BLK-SL | Market Block | North Campus | Orion Tower | Floor 1 | West Wing | North Campus, Orion Tower, Floor 1, West Wing | Sales operations and regional coordination | 1 |
| 5 | BLK-MK | Brand Block | North Campus | Orion Tower | Floor 2 | Creative Wing | North Campus, Orion Tower, Floor 2, Creative Wing | Marketing campaign and creative workspace | 1 |
| 6 | BLK-EX | Executive Block | Main Campus | Cedar Tower | Floor 5 | Executive Wing | Main Campus, Cedar Tower, Floor 5, Executive Wing | Leadership, executive reviews, and cross-functional decisions | 1 |

## DepartmentBlocks

| DepartmentId | DepartmentName | BlockId | BlockCode | BlockName | IsPrimary | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | Engineering | 1 | BLK-A | Atria Block | 1 | Primary Engineering workspace |
| 1 | Engineering | 6 | BLK-EX | Executive Block | 0 | Shared leadership review rooms |
| 3 | Finance | 6 | BLK-EX | Executive Block | 0 | Executive finance review rooms |
| 3 | Finance | 3 | BLK-FN | Ledger Block | 1 | Primary Finance workspace |
| 2 | Human Resources | 2 | BLK-HR | People Block | 1 | Primary HR block and employee support desk |
| 5 | Marketing | 5 | BLK-MK | Brand Block | 1 | Primary Marketing workspace |
| 4 | Sales | 6 | BLK-EX | Executive Block | 0 | Executive sales review rooms |
| 4 | Sales | 4 | BLK-SL | Market Block | 1 | Primary Sales workspace |

## Employees

| EmployeeId | Name | Email | DepartmentId | DepartmentName | BlockId | BlockCode | BlockName | SeatNumber | DeskLocation |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | Aarav Mehta | aarav.mehta@contoso.example | 1 | Engineering | 1 | BLK-A | Atria Block | A-101 | Pod A1, desk 01 |
| 2 | Sophia Carter | sophia.carter@contoso.example | 1 | Engineering | 1 | BLK-A | Atria Block | A-102 | Pod A1, desk 02 |
| 3 | Liam Johnson | liam.johnson@contoso.example | 1 | Engineering | 1 | BLK-A | Atria Block | A-103 | Pod A1, desk 03 |
| 4 | Priya Nair | priya.nair@contoso.example | 1 | Engineering | 1 | BLK-A | Atria Block | A-104 | Pod A2, desk 04 |
| 5 | Noah Williams | noah.williams@contoso.example | 2 | Human Resources | 2 | BLK-HR | People Block | HR-201 | HR service row, desk 01 |
| 6 | Emma Brown | emma.brown@contoso.example | 2 | Human Resources | 2 | BLK-HR | People Block | HR-202 | HR service row, desk 02 |
| 7 | Rohan Desai | rohan.desai@contoso.example | 2 | Human Resources | 2 | BLK-HR | People Block | HR-203 | HR service row, desk 03 |
| 8 | Olivia Davis | olivia.davis@contoso.example | 3 | Finance | 3 | BLK-FN | Ledger Block | F-301 | Finance pod, desk 01 |
| 9 | Mia Wilson | mia.wilson@contoso.example | 3 | Finance | 3 | BLK-FN | Ledger Block | F-302 | Finance pod, desk 02 |
| 10 | Ethan Miller | ethan.miller@contoso.example | 3 | Finance | 3 | BLK-FN | Ledger Block | F-303 | Finance pod, desk 03 |
| 11 | Ananya Rao | ananya.rao@contoso.example | 3 | Finance | 3 | BLK-FN | Ledger Block | F-304 | Finance planning row, desk 04 |
| 12 | James Taylor | james.taylor@contoso.example | 4 | Sales | 4 | BLK-SL | Market Block | S-101 | Sales west row, desk 01 |
| 13 | Isabella Anderson | isabella.anderson@contoso.example | 4 | Sales | 4 | BLK-SL | Market Block | S-102 | Sales west row, desk 02 |
| 14 | Lucas Thomas | lucas.thomas@contoso.example | 4 | Sales | 4 | BLK-SL | Market Block | S-103 | Sales west row, desk 03 |
| 15 | Neha Sharma | neha.sharma@contoso.example | 4 | Sales | 4 | BLK-SL | Market Block | S-104 | Sales west row, desk 04 |
| 16 | Benjamin Moore | benjamin.moore@contoso.example | 5 | Marketing | 5 | BLK-MK | Brand Block | M-201 | Creative pod, desk 01 |
| 17 | Charlotte Martin | charlotte.martin@contoso.example | 5 | Marketing | 5 | BLK-MK | Brand Block | M-202 | Creative pod, desk 02 |
| 18 | Kabir Singh | kabir.singh@contoso.example | 5 | Marketing | 5 | BLK-MK | Brand Block | M-203 | Campaign row, desk 03 |
| 19 | Amelia White | amelia.white@contoso.example | 5 | Marketing | 5 | BLK-MK | Brand Block | M-204 | Campaign row, desk 04 |
| 20 | Daniel Clark | daniel.clark@contoso.example | 1 | Engineering | 1 | BLK-A | Atria Block | A-105 | Platform row, desk 05 |
| 21 | Arun | arun@company.com | 1 | Engineering | 1 | BLK-A | Atria Block | A-106 | Platform row, desk 06 |
| 22 | Kiran | kiran@company.com | 1 | Engineering | 1 | BLK-A | Atria Block | A-107 | Platform row, desk 07 |
| 23 | Arun Manager | arun.manager@company.com | 1 | Engineering | 6 | BLK-EX | Executive Block | EX-501 | Executive engineering office |
| 24 | Priya HR | priya.hr@company.com | 2 | Human Resources | 2 | BLK-HR | People Block | HR-204 | HR leadership office |
| 25 | Rahul Director | rahul.director@company.com | 4 | Sales | 6 | BLK-EX | Executive Block | EX-502 | Executive sales office |
| 26 | CEO Admin | ceo@company.com | 3 | Finance | 6 | BLK-EX | Executive Block | EX-503 | Executive finance office |

## EmployeeBlocks

| EmployeeId | EmployeeName | BlockId | BlockCode | BlockName | SeatNumber | DeskLocation | AssignedFrom |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | Aarav Mehta | 1 | BLK-A | Atria Block | A-101 | Pod A1, desk 01 | 2026-01-01 |
| 2 | Sophia Carter | 1 | BLK-A | Atria Block | A-102 | Pod A1, desk 02 | 2026-01-01 |
| 3 | Liam Johnson | 1 | BLK-A | Atria Block | A-103 | Pod A1, desk 03 | 2026-01-01 |
| 4 | Priya Nair | 1 | BLK-A | Atria Block | A-104 | Pod A2, desk 04 | 2026-01-01 |
| 5 | Noah Williams | 2 | BLK-HR | People Block | HR-201 | HR service row, desk 01 | 2026-01-01 |
| 6 | Emma Brown | 2 | BLK-HR | People Block | HR-202 | HR service row, desk 02 | 2026-01-01 |
| 7 | Rohan Desai | 2 | BLK-HR | People Block | HR-203 | HR service row, desk 03 | 2026-01-01 |
| 8 | Olivia Davis | 3 | BLK-FN | Ledger Block | F-301 | Finance pod, desk 01 | 2026-01-01 |
| 9 | Mia Wilson | 3 | BLK-FN | Ledger Block | F-302 | Finance pod, desk 02 | 2026-01-01 |
| 10 | Ethan Miller | 3 | BLK-FN | Ledger Block | F-303 | Finance pod, desk 03 | 2026-01-01 |
| 11 | Ananya Rao | 3 | BLK-FN | Ledger Block | F-304 | Finance planning row, desk 04 | 2026-01-01 |
| 12 | James Taylor | 4 | BLK-SL | Market Block | S-101 | Sales west row, desk 01 | 2026-01-01 |
| 13 | Isabella Anderson | 4 | BLK-SL | Market Block | S-102 | Sales west row, desk 02 | 2026-01-01 |
| 14 | Lucas Thomas | 4 | BLK-SL | Market Block | S-103 | Sales west row, desk 03 | 2026-01-01 |
| 15 | Neha Sharma | 4 | BLK-SL | Market Block | S-104 | Sales west row, desk 04 | 2026-01-01 |
| 16 | Benjamin Moore | 5 | BLK-MK | Brand Block | M-201 | Creative pod, desk 01 | 2026-01-01 |
| 17 | Charlotte Martin | 5 | BLK-MK | Brand Block | M-202 | Creative pod, desk 02 | 2026-01-01 |
| 18 | Kabir Singh | 5 | BLK-MK | Brand Block | M-203 | Campaign row, desk 03 | 2026-01-01 |
| 19 | Amelia White | 5 | BLK-MK | Brand Block | M-204 | Campaign row, desk 04 | 2026-01-01 |
| 20 | Daniel Clark | 1 | BLK-A | Atria Block | A-105 | Platform row, desk 05 | 2026-01-01 |
| 21 | Arun | 1 | BLK-A | Atria Block | A-106 | Platform row, desk 06 | 2026-01-01 |
| 22 | Kiran | 1 | BLK-A | Atria Block | A-107 | Platform row, desk 07 | 2026-01-01 |
| 23 | Arun Manager | 6 | BLK-EX | Executive Block | EX-501 | Executive engineering office | 2026-01-01 |
| 24 | Priya HR | 2 | BLK-HR | People Block | HR-204 | HR leadership office | 2026-01-01 |
| 25 | Rahul Director | 6 | BLK-EX | Executive Block | EX-502 | Executive sales office | 2026-01-01 |
| 26 | CEO Admin | 6 | BLK-EX | Executive Block | EX-503 | Executive finance office | 2026-01-01 |

## Projects

| ProjectId | ProjectName | Status |
| --- | --- | --- |
| 1 | Atlas Platform Modernization | Active |
| 2 | Benefits Portal Refresh | Active |
| 3 | Quarterly Forecast Automation | Active |
| 4 | North Region CRM Rollout | Active |
| 5 | Brand Awareness Campaign | Active |
| 6 | Data Warehouse Migration | Planning |
| 7 | Security Compliance Review | Active |
| 8 | Customer Insights Dashboard | Completed |
| 9 | Payroll Integration | On Hold |
| 10 | Partner Enablement Toolkit | Active |

## EmployeeProjects

| EmployeeId | EmployeeName | ProjectId | ProjectName | Status |
| --- | --- | --- | --- | --- |
| 1 | Aarav Mehta | 1 | Atlas Platform Modernization | Active |
| 2 | Sophia Carter | 1 | Atlas Platform Modernization | Active |
| 3 | Liam Johnson | 6 | Data Warehouse Migration | Planning |
| 4 | Priya Nair | 7 | Security Compliance Review | Active |
| 5 | Noah Williams | 2 | Benefits Portal Refresh | Active |
| 6 | Emma Brown | 2 | Benefits Portal Refresh | Active |
| 7 | Rohan Desai | 9 | Payroll Integration | On Hold |
| 8 | Olivia Davis | 3 | Quarterly Forecast Automation | Active |
| 9 | Mia Wilson | 3 | Quarterly Forecast Automation | Active |
| 10 | Ethan Miller | 8 | Customer Insights Dashboard | Completed |
| 11 | Ananya Rao | 7 | Security Compliance Review | Active |
| 12 | James Taylor | 4 | North Region CRM Rollout | Active |
| 13 | Isabella Anderson | 4 | North Region CRM Rollout | Active |
| 14 | Lucas Thomas | 10 | Partner Enablement Toolkit | Active |
| 15 | Neha Sharma | 10 | Partner Enablement Toolkit | Active |
| 16 | Benjamin Moore | 5 | Brand Awareness Campaign | Active |
| 17 | Charlotte Martin | 5 | Brand Awareness Campaign | Active |
| 18 | Kabir Singh | 8 | Customer Insights Dashboard | Completed |
| 19 | Amelia White | 10 | Partner Enablement Toolkit | Active |
| 20 | Daniel Clark | 1 | Atlas Platform Modernization | Active |
| 21 | Arun | 1 | Atlas Platform Modernization | Active |
| 22 | Kiran | 1 | Atlas Platform Modernization | Active |
| 23 | Arun Manager | 1 | Atlas Platform Modernization | Active |
| 24 | Priya HR | 2 | Benefits Portal Refresh | Active |
| 25 | Rahul Director | 10 | Partner Enablement Toolkit | Active |
| 26 | CEO Admin | 7 | Security Compliance Review | Active |

