English | [Türkçe](README.tr.md)

# Tamirhane

Work order tracking for repair shops and service centers: device intake, repair status, quotes and customer approval, parts inventory, warranty tracking and a login-free tracking link for customers. Built with ASP.NET Core MVC on Clean Architecture.

> **Status:** In development. This document describes the plan; features are checked off as they are completed.

## Features

**Shops and users**
- [ ] Multi-tenant sign-up: each shop's data is fully isolated
- [ ] Branches: inventory and work orders per branch
- [ ] Roles: owner and technician; staff invitations by email
- [ ] Sign-up with email verification, password reset

**Device intake**
- [ ] Customer and device records: brand, model, serial number / IMEI, accessories received, visible condition, customer's complaint
- [ ] Printable A5 intake receipt with a QR code to the tracking link
- [ ] Barcode / QR label to stick on the device

**Work orders**
- [ ] Status flow: Received → Diagnosis → Awaiting approval → In repair → Ready → Delivered; cancel and return. Invalid transitions are blocked
- [ ] Quotes: parts and labor lines, VAT; the customer approves or declines from the tracking link
- [ ] Parts inventory: used parts are deducted, low-stock warning
- [ ] Payment records: cash, card, bank transfer; outstanding balance
- [ ] Warranty: the warranty period starts at delivery; a device returning under warranty is linked to its previous work order

**Customer communication**
- [ ] Login-free tracking page: unguessable code, status timeline
- [ ] Email notification on every status change
- [ ] Browser notifications (Web Push): the customer opts in on the tracking page
- [ ] "Notify via WhatsApp" button: one click with a ready message and the tracking link

**Dashboard**
- [ ] Open jobs, awaiting approval, ready for pickup, monthly revenue
- [ ] Turkish and English, light and dark theme
- [ ] Public demo shop with sample data

## Architecture

Clean Architecture: dependencies only point inward.

```
Tamirhane.Web  ──►  Tamirhane.Application  ──►  Tamirhane.Domain
      │                      ▲
      └──► Tamirhane.Infrastructure (only to register services in Program.cs)
```

| Project | Responsibility |
|---|---|
| `Tamirhane.Domain` | Entities and business rules (e.g. the work order status flow). Depends on no other project or framework. |
| `Tamirhane.Application` | Use cases; interfaces for external services such as storage, email and the clock. |
| `Tamirhane.Infrastructure` | Implementations of those interfaces: EF Core + PostgreSQL, email, Web Push. |
| `Tamirhane.Web` | ASP.NET Core MVC: controllers, views, authentication. |

The dependency rule is verified by tests in `Tamirhane.Architecture.Tests`.

## Tech Stack

- C#, ASP.NET Core MVC, Entity Framework Core, PostgreSQL
- ASP.NET Core Identity
- xUnit, NetArchTest
- Docker, Render

## Installation

To be written.

## What I Learned

To be written when the project is finished.
