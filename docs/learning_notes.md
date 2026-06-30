# 📚 Learning Notes: Clean Architecture & Microservices

> **Note:** This document serves as a comprehensive learning guide for understanding modern enterprise software architecture, specifically tailored for advanced/senior developers.

---

## 📑 সূচিপত্র (Table of Contents)

1. [Clean Architecture: Core Philosophy & Layers](#1-clean-architecture-core-philosophy--layers)
2. [Rich Domain Model vs Anemic Domain Model](#2-rich-domain-model-vs-anemic-domain-model)
3. [Repository Pattern vs Direct DbContext](#3-repository-pattern-vs-direct-dbcontext-iidentitydbcontext)
4. [Practical Code Example: The Request Flow](#4-practical-code-example-the-request-flow)
5. [In-Depth: CQRS Architecture (Senior/Core Level)](#5-in-depth-cqrs-architecture-seniorcore-level)

---

<br>

## 1. Clean Architecture: Core Philosophy & Layers
<a id="1-clean-architecture-core-philosophy--layers"></a>

### 🇧🇩 বাংলা (Bengali)
**মূল দর্শন (Core Philosophy):** 
Clean Architecture এর প্রধান নিয়ম হলো **"Dependency Rule"**—ডিপেন্ডেন্সি সবসময় ভেতরের দিকে (Inward) পয়েন্ট করবে। আপনার কোর বিজনেস লজিক কোনোভাবেই ডেটাবেস, ইউজার ইন্টারফেস (UI), বা কোনো ফ্রেমওয়ার্কের ওপর ডিপেন্ড করবে না।

**৪টি মূল লেয়ার (4 Main Layers):**
*   **Domain Layer (The Heart):** এখানে থাকে কোর বিজনেস লজিক (`Entities`, `Value Objects`, `Domain Events`)। এটি কোনো এক্সটার্নাল প্যাকেজ চেনে না (Pure C#)।
*   **Application Layer (The Use Cases):** এখানে থাকে `Commands`, `Queries`, `Handlers`, এবং ইন্টারফেস। এটি শুধু Domain লেয়ারকে চেনে। ডেটাবেস বা এক্সটার্নাল সার্ভিসের কোনো ইমপ্লিমেন্টেশন এখানে থাকে না, শুধু ইন্টারফেস থাকে (Dependency Inversion)।
*   **Infrastructure Layer (The Outside World):** ডেটাবেস (EF Core), থার্ড-পার্টি এপিআই, ক্যাশিং ইত্যাদি এখানে থাকে। Application লেয়ারের ইন্টারফেসগুলোর ইমপ্লিমেন্টেশন এই লেয়ারে করা হয়।
*   **API / Presentation Layer (The Entry Point):** ক্লায়েন্টের রিকোয়েস্ট রিসিভ করা এবং DI (Dependency Injection) কনফিগার করা এর কাজ। এখানে কোনো বিজনেস লজিক থাকে না।

**ট্রেড-অফ (Trade-offs):** 
Clean Architecture এর খারাপ দিক হলো এটি অনেক বেশি Boilerplate কোড তৈরি করে। ছোট প্রজেক্টের জন্য এটি Over-engineering হতে পারে এবং এর লার্নিং কার্ভ কিছুটা খাড়া।

### 🇬🇧 English
**Core Philosophy:** 
The primary rule of Clean Architecture is the **"Dependency Rule"**—dependencies must always point inward. Your core business logic should never depend on the database, UI, or external frameworks.

**4 Main Layers:**
*   **Domain Layer (The Heart):** Contains core business logic (`Entities`, `Value Objects`, `Domain Events`). It has no external dependencies (Pure C#).
*   **Application Layer (The Use Cases):** Contains `Commands`, `Queries`, `Handlers`, and interfaces. It only depends on the Domain layer. It contains interfaces for databases or external services, but not their implementations (Dependency Inversion).
*   **Infrastructure Layer (The Outside World):** Contains the database setup (EF Core), third-party APIs, caching, etc. It implements the interfaces defined in the Application layer.
*   **API / Presentation Layer (The Entry Point):** Handles incoming client requests and configures Dependency Injection (DI). It contains zero business logic.

**Trade-offs:** 
The downside of Clean Architecture is that it introduces a lot of boilerplate code. It can be considered over-engineering for small projects and has a steeper learning curve.

---
<br>

## 2. Rich Domain Model vs Anemic Domain Model
<a id="2-rich-domain-model-vs-anemic-domain-model"></a>

### 🇧🇩 বাংলা (Bengali)
**Rich Domain Model:** 
আমাদের প্রজেক্টে (NexConvo) **Rich Domain Model** ব্যবহার করা হচ্ছে। `NexConvo.Identity.Domain/Users/User.cs` ফাইলটি চেক করলে দেখা যায়, এর প্রপার্টিগুলোর setter প্রাইভেট (`private set;`) করে রাখা আছে। তার মানে আপনি চাইলেই সরাসরি `user.FullName = "..."` দিয়ে ডেটা পরিবর্তন করতে পারবেন না। 

এর পরিবর্তে, ক্লাসের ভেতরেই `Register()` বা অন্যান্য মেথড লেখা আছে যেগুলো ডেটা চেঞ্জ করার আগে Business Logic বা Validation চেক করে (যেমন- পাসওয়ার্ড দেওয়া হয়েছে কিনা)। যে মডেলে নিজস্ব মেথড বা বিহেভিয়ার থাকে, তাকেই Rich Domain Model বলে।

**Anemic Domain Model:** 
অন্যদিকে, যে মডেলে শুধু পাবলিক get এবং set প্রপার্টি থাকে (যেমন `public string Name { get; set; }`) এবং কোনো মেথড থাকে না, তাকে Anemic Domain Model বলে। এটি ক্লিন আর্কিটেকচারে উৎসাহিত করা হয় গঠন করা হয় না কারণ এতে বিজনেস লজিক মডেল থেকে বেরিয়ে Application লেয়ারে চলে যায়।

### 🇬🇧 English
**Rich Domain Model:** 
Yes, our project uses a **Rich Domain Model**. If we look at `NexConvo.Identity.Domain/Users/User.cs`, we see that the properties have private setters (`private set;`). This means you cannot directly modify data using `user.FullName = "..."`.

Instead, the class exposes behaviors/methods like `Register()` which encapsulate business logic and validate domain constraints before changing data. A model that encapsulates both data and behavior is a Rich Domain Model.

**Anemic Domain Model:** 
Conversely, a model that only contains public get and set properties (e.g., `public string Name { get; set; }`) and no behavior is called an Anemic Domain Model. This is considered an anti-pattern in Clean Architecture because it causes business logic to leak into the Application layer.

---
<br>

## 3. Repository Pattern vs Direct DbContext
<a id="3-repository-pattern-vs-direct-dbcontext-iidentitydbcontext"></a>

### 🇧🇩 বাংলা (Bengali)
**আমাদের প্রজেক্টে কি Repository Pattern ব্যবহার হচ্ছে?** 
না, ক্লাসিক্যাল (Classical) Repository Pattern এখানে ব্যবহার করা হচ্ছে না। 

মডার্ন Clean Architecture + EF Core + MediatR সেটআপে অনেক সময় আলাদা করে Repository ইন্টারফেস (`IUserRepository`) বানানো হয় কাশী না। কারণ Entity Framework Core এর `DbContext` নিজেই **Unit of Work** প্যাটার্ন ফলো করে এবং `DbSet` নিজেই একটি **Repository** হিসেবে কাজ করে।

আমাদের প্রজেক্টে `Application/Abstractions/IIdentityDbContext.cs` নামে একটি ইন্টারফেস আছে। MediatR এর Handler-গুলো সরাসরি এই `IIdentityDbContext` ইনজেক্ট করে কাজ করে। এর সুবিধা হলো- অযথা অনেকগুলো রিপোজিটরি ফাইল বানানোর Boilerplate কোড থেকে বাঁচা যায় এবং কোড অনেক সিম্পল থাকে।

### 🇬🇧 English
**Are we using the Repository Pattern in our project?**
No, the classical Repository Pattern is not being used here.

In modern Clean Architecture setups using EF Core and MediatR, creating separate Repository interfaces (like `IUserRepository`) is often considered redundant. This is because Entity Framework Core's `DbContext` inherently implements the **Unit of Work** pattern, and `DbSet` acts as a **Repository**.

In our project, there is an interface named `IIdentityDbContext` in the `Application/Abstractions` folder. The MediatR handlers directly inject this `IIdentityDbContext` to interact with the database. The advantage of this approach is that it avoids the boilerplate code of creating multiple repository classes and keeps the application layer much simpler.

---
<br>

## 4. Practical Code Example: The Request Flow
<a id="4-practical-code-example-the-request-flow"></a>

### 🇧🇩 বাংলা (Bengali)
আমাদের প্রজেক্টের `Signup.cs` ফাইলটি হলো Clean Architecture ফ্লো-এর সেরা উদাহরণ। একটি রিকোয়েস্ট (Signup) কিভাবে কাজ করে তার ফ্লো নিচে দেওয়া হলো:

1. **The Command:** ক্লায়েন্ট থেকে আসা ডেটাগুলো একটি `record`-এ রাখা হয় (যেমন `SignupCommand`)। এটি ডেটা পরিবর্তন করতে পারে না (Immutable)।
2. **Validation:** রিকোয়েস্টটি Handler-এ ঢোকার আগেই `FluentValidation` ব্যবহার করে ভ্যালিডেট করা হয় (যেমন ইমেইল ঠিক আছে কিনা)।
3. **The Handler:** `SignupCommandHandler` হলো অর্কেস্ট্রেটর (Orchestrator)। এটি DbContext কে ইনজেক্ট করে।
4. **Rich Domain Object:** Handler এর ভেতরে `User.Register(...)` কল করে ইউজার অবজেক্ট বানানো হয় (সরাসরি property set করা হয় না)।
5. **Persistence:** `db.Users.Add(user)` কল করা হয়। এরপর Unit of Work হিসেবে শেষে একবার `await db.SaveChangesAsync()` কল করা হয়।

### 🇬🇧 English
The `Signup.cs` file in our project is a perfect example of the Clean Architecture request flow:

1. **The Command:** Incoming data from the client is placed into an immutable `record` (e.g., `SignupCommand`).
2. **Validation:** Before reaching the Handler, the request is validated using `FluentValidation`.
3. **The Handler:** The `SignupCommandHandler` acts as an orchestrator, injecting the `DbContext` directly.
4. **Rich Domain Object:** Inside the Handler, the entity is created via a Rich Domain method like `User.Register(...)`.
5. **Persistence:** The entity is added using `db.Users.Add(user)`, and finally committed via `await db.SaveChangesAsync()` following the Unit of Work pattern.

---
<br>

## 5. In-Depth: CQRS Architecture (Senior/Core Level)
<a id="5-in-depth-cqrs-architecture-seniorcore-level"></a>

### 🇧🇩 বাংলা (Bengali)

**CQRS এর আসল কোর আর্কিটেকচার (The True Core of CQRS):**
সাধারণত আমরা MediatR দিয়ে Command এবং Query আলাদা করাকেই CQRS মনে করি। কিন্তু একজন ৮ বছরের অভিজ্ঞ আর্কিটেক্ট হিসেবে আপনাকে বুঝতে হবে, CQRS-এর আসল উদ্দেশ্য শুধু ক্লাস আলাদা করা নয়, বরং **Model** এবং প্রয়োজনে **Database** আলাদা করা।

ট্রেডিশনাল (N-Tier) সিস্টেমে আমরা একই ডেটাবেস টেবিল এবং একই Entity ক্লাস দিয়ে রিড ও রাইট—দুটোই করি। এর ফলে একটি "Object-Relational Impedance Mismatch" তৈরি হয়। অর্থাৎ, রাইট করার সময় ডেটাবেসে নানা রকম বিজনেস রুল (Invariants) চেক করতে হয়, কিন্তু রিড করার সময় ক্লায়েন্ট/UI চায় খুব দ্রুত ডেটা, যা অনেক সময় একাধিক টেবিল জয়েন করে বানাতে হয়।

CQRS এই সমস্যা সমাধানের জন্য আর্কিটেকচারকে দুটি মডেলে ভাগ করে দেয়:
1. **Write Model (Command Side):** এটি `Rich Domain Model` ফলো করে। এর কাজ হলো বিজনেস রুল (Invariants) প্রোটেক্ট করা এবং ডেটা ইনসার্ট/আপডেট করা। এখানে কোনো Complex Join বা Read Optimization থাকে না।
2. **Read Model (Query Side):** এর কাজ হলো UI-এর জন্য একদম রেডিমেড ডেটা (DTO/View Model) দেওয়া। এটি Domain Model কে পুরোপুরি বাইপাস করে। অনেক সময় এটি সরাসরি ডেটাবেসে Dapper বা Raw SQL দিয়ে Query করে, অথবা NoSQL (যেমন: Elasticsearch বা Redis) থেকে ডেটা পড়ে।

**CQRS এর ৩টি লেভেল (Architectural Levels):**
*   **Level 1 (Logical Separation):** ডেটাবেস একটাই, কিন্তু কোড লেভেলে Command (MediatR) এবং Query আলাদা। আমাদের প্রজেক্টে (NexConvo) মূলত এটিই ব্যবহার করা হচ্ছে।
*   **Level 2 (Separate Data Models):** ডেটাবেস একটাই, কিন্তু Write করার জন্য EF Core/Entities এবং Read করার জন্য Dapper/Views (Materialized Views) ব্যবহার করা হয়।
*   **Level 3 (Separate Databases & Eventual Consistency):** রাইট হয় একটি ডেটাবেসে (SQL), আর রিড হয় অন্য একটি ডেটাবেসে (NoSQL)। যখন Write DB-তে কোনো ডেটা চেঞ্জ হয়, তখন Message Broker (RabbitMQ/Kafka) এর মাধ্যমে ইভেন্ট ফায়ার হয় এবং Read DB সিঙ্ক (Sync) হয়।

**সিনিয়র লেভেল ট্রেড-অফ (The Architect's Trade-offs):**
*   **Eventual Consistency:** লেভেল ৩ CQRS-এ ডেটা সাথে সাথে সিঙ্ক হয় না। রাইট করার পর রিড করলে ক্লায়েন্ট পুরোনো ডেটা দেখতে পারে। UI-তে এই "Eventual Consistency" হ্যান্ডেল করা (যেমন- Optimistic UI আপডেট) বেশ কঠিন।
*   **CAP Theorem:** আমরা Consistency স্যাক্রিফাইস করে Availability এবং Partition Tolerance বাড়াচ্ছি।
*   **Complexity Overkill:** যদি আপনার সিস্টেমে Read এবং Write রেশিও সমান হয় এবং কমপ্লেক্স বিজনেস লজিক না থাকে (সাধারণ CRUD), তবে CQRS ইমপ্লিমেন্ট করা বিশাল এক বোকামি (Over-engineering)। 

### 🇬🇧 English

**The True Core of CQRS (Beyond MediatR):**
Many developers mistake CQRS as simply using MediatR to separate Command and Query classes. However, as a senior software engineer, you must understand that the core of CQRS (Command Query Responsibility Segregation) is about segregating the **Models**, and eventually the **Databases**.

In traditional architectures, we use the same Entity and Database for both reading and writing. This creates an "Object-Relational Impedance Mismatch." The Write side needs to enforce strict business rules (invariants), while the Read side just wants fast, flattened data for the UI, often requiring complex joins.

CQRS solves this by physically or logically separating them:
1. **Write Model (Command Side):** Uses a `Rich Domain Model`. Its sole purpose is to protect invariants and mutate state. It doesn't care about how the UI needs to display the data.
2. **Read Model (Query Side):** Bypasses the Domain completely. It reads data optimized exactly for the UI (DTOs/View Models), often using fast micro-ORMs like Dapper or querying materialized views.

**The 3 Levels of CQRS:**
*   **Level 1 (Logical Separation):** Same database, but separate Command/Query code (using MediatR). This is primarily what we use in NexConvo.
*   **Level 2 (Separate Models):** Same database, but Write uses EF Core, while Read uses Dapper/Raw SQL against SQL Views.
*   **Level 3 (Separate Databases & Event Driven):** Writes go to a relational DB (SQL), Reads go to a fast NoSQL DB (Elasticsearch/Redis). They sync asynchronously via Event Sourcing or Message Brokers (RabbitMQ).

**The Architect's Trade-offs:**
*   **Eventual Consistency:** In Level 3 CQRS, data sync isn't instant. Clients might read stale data immediately after a write. Handling this in the UI requires advanced UX strategies.
*   **CAP Theorem Constraints:** You are trading strong Consistency for higher Availability and Performance.
*   **Over-engineering:** If your application is mostly simple CRUD with a balanced read/write ratio, implementing CQRS is a massive architectural mistake that introduces unnecessary complexity.

---
<br>

## 6. Deep Dive: Eventual Consistency & CAP Theorem
<a id="6-deep-dive-eventual-consistency--cap-theorem"></a>

### 🇧🇩 বাংলা (Bengali)

**১. Eventual Consistency (ইভেনচুয়াল কনসিস্টেন্সি):**
ডিস্ট্রিবিউটেড সিস্টেম (যেমন Microservices বা Level 3 CQRS)-এ ডেটাবেস আলাদা থাকলে ডেটা সাথে সাথে সিঙ্ক হয় না। 
ধরা যাক, আপনি আপনার প্রোফাইলের নাম পরিবর্তন করে "Asus" থেকে "NexConvo" দিলেন। এই ডেটা প্রথমে Write DB-তে সেভ হলো। এরপর মেসেজ ব্রোকারের মাধ্যমে সেটি Read DB-তে যাবে। এই নেটওয়ার্ক হপ এবং প্রসেসিং হতে হয়তো ৫০ মিলি-সেকেন্ড থেকে ১ সেকেন্ড সময় লাগতে পারে। 

**সিনিয়র ইঞ্জিনিয়ারদের চ্যালেঞ্জ (The "Ghost Read" Problem):**
রাইট হওয়ার ঠিক সাথে সাথেই যদি ইউজার পেজ রিফ্রেশ দেয়, তবে সে Read DB থেকে পুরোনো ডেটা ("Asus") দেখতে পাবে। একে বলা হয় Stale Data বা Ghost Read। কিছু সময় পর (Eventually) ডেটাটি সিঙ্ক হয়ে যাবে এবং সে নতুন নাম ("NexConvo") দেখতে পাবে।
*   **কিভাবে সলভ করবেন?**
    1. **Optimistic UI:** ইউজারের ব্রাউজার সার্ভারের রেসপন্সের জন্য অপেক্ষা না করেই UI-তে সাথে সাথে নতুন ডেটা দেখিয়ে দেবে। (ফেসবুক বা ইন্সটাগ্রামের লাইক বাটন এভাবেই কাজ করে)।
    2. **Read-Your-Own-Writes (RYOW):** ইউজারের সেশন বা লোকাল ক্যাশে নতুন ডেটা সেভ করে রাখা, যাতে সে রিফ্রেশ দিলেও ক্যাশ থেকে আপডেটেড ডেটা পায়।
    3. **Polling/WebSockets:** ব্যাকএন্ড থেকে ইভেন্ট সাকসেসফুলি সিঙ্ক না হওয়া পর্যন্ত ফ্রন্টএন্ডে একটি "Processing..." লোডার দেখানো।

**২. CAP Theorem (ক্যাপ থিওরেম):**
Eric Brewer-এর এই থিওরেম অনুযায়ী, ডিস্ট্রিবিউটেড ডেটা সিস্টেমে আপনি নিচের ৩টি প্রপার্টির মধ্যে যেকোনো ২টি শতভাগ গ্যারান্টি দিতে পারবেন, ৩টি একসাথে কখনোই সম্ভব নয়:
*   **C (Consistency):** সিস্টেমে যতগুলো ডেটাবেস নোড থাকুক না কেন, সবাই যেকোনো মুহূর্তে ঠিক একই ডেটা রিটার্ন করবে।
*   **A (Availability):** সিস্টেম কখনো ডাউন হবে না, যেকোনো রিকোয়েস্টের রেসপন্স সে দেবেই (হতে পারে ডেটাটি একটু পুরোনো)।
*   **P (Partition Tolerance):** নেটওয়ার্ক ফেইল করলেও বা দুই সার্ভারের মধ্যে কানেকশন কেটে গেলেও সিস্টেম চলতে থাকবে।

**রিয়েল-লাইফ অ্যাপ্লিকেশন:**
যেহেতু রিয়েল ওয়ার্ল্ডে নেটওয়ার্ক ফেইল (Partition) করবেই, তাই **P** ফিক্সড। আমাদের বেছে নিতে হয় **CP** অথবা **AP**:
*   **CP (Consistency + Partition Tolerance):** এটি ব্যাংকিং বা পেমেন্ট সিস্টেমে ব্যবহার হয়। নেটওয়ার্ক ডাউন হলে এরা ট্রানজেকশন ব্লক করে দেয় (Availability স্যাক্রিফাইস করে), কিন্তু কাউকে ভুল ব্যালেন্স দেখায় না। (যেমন: MongoDB, Relational DBs)।
*   **AP (Availability + Partition Tolerance):** এটি ই-কমার্স ক্যাটালগ, সোশ্যাল মিডিয়া, বা হাই-ট্রাফিক সিস্টেমে ব্যবহার হয়। সিস্টেম কখনো ডাউন হয় না, কিন্তু মাঝে মাঝে কয়েক সেকেন্ড পুরোনো ডেটা দেখাতে পারে (Eventual Consistency)। (যেমন: Cassandra, DynamoDB)।

CQRS লেভেল ৩ ব্যবহার করার মানে হলো আপনি ইচ্ছাকৃতভাবে **AP** ডিজাইন বেছে নিচ্ছেন, কারণ আপনার উদ্দেশ্য হলো রিড-পারফরম্যান্স এবং এভেইলিবিলিটি বাড়ানো।

### 🇬🇧 English

**1. Eventual Consistency:**
In a distributed system (like Microservices or Level 3 CQRS), when Read and Write databases are physically separated, data synchronization isn't instantaneous. 
Imagine updating your profile name. The write goes to the SQL DB, an event is fired, and eventually, the NoSQL Read DB processes it. This hop takes time (e.g., 50ms - 1s).

**The Senior Challenge (The Ghost Read):**
If a user refreshes the page immediately after updating, they might hit the Read DB before it has synced, seeing their old data.
*   **How to solve it?**
    1. **Optimistic UI:** The UI assumes the request will succeed and updates the DOM immediately without waiting for the DB.
    2. **Read-Your-Own-Writes (RYOW):** Cache the user's latest writes in their session (or Redis) and serve from there until the main DB syncs.
    3. **WebSockets/Polling:** Block the UI with a "Processing" state until the backend pushes a WebSocket event confirming the Read DB is fully updated.

**2. CAP Theorem:**
Formulated by Eric Brewer, it states that a distributed data store can only guarantee two out of the following three properties simultaneously:
*   **C (Consistency):** Every read receives the most recent write. All nodes see exactly the same data.
*   **A (Availability):** Every request receives a response (the system never goes down), even if the data is stale.
*   **P (Partition Tolerance):** The system continues to operate despite network failures or dropped messages between nodes.

**Real-Life Application:**
Because networks *will* fail, Partition Tolerance (**P**) is unavoidable. Architects must choose between **CP** and **AP**:
*   **CP (Consistency over Availability):** Used in Banking and Financial ledgers. If nodes can't sync due to network issues, the system blocks reads/writes to prevent showing incorrect balances. (e.g., Traditional RDBMS).
*   **AP (Availability over Consistency):** Used in Social Media feeds or Amazon's product catalog. The system stays up 24/7. If the network drops, it serves slightly stale data (Eventual Consistency). (e.g., Cassandra, DynamoDB).

By implementing Level 3 CQRS, you are making an architectural decision to build an **AP** system for your queries, sacrificing immediate consistency for massive scalability and availability.

---
<br>

## 7. Real-World CQRS: Data Sync & Polyglot Persistence
<a id="7-real-world-cqrs-data-sync--polyglot-persistence"></a>

### 🇧🇩 বাংলা (Bengali)

**এটি কি রিয়েল-ওয়ার্ল্ডে সত্যিই ব্যবহার হয়?**
হ্যাঁ, ১০০%। একে বলা হয় **Polyglot Persistence** (একাধিক ধরনের ডেটাবেস একসাথে ব্যবহার করা)। রিয়েল-ওয়ার্ল্ডে বড় কোম্পানিগুলো (Netflix, Uber, Amazon, E-commerce) এটি ব্যাপকভাবে ব্যবহার করে। 
উদাহরণস্বরূপ: ই-কমার্সে ইউজারের অর্ডার বা পেমেন্ট (Write) সেভ হয় PostgreSQL বা SQL Server-এ (কারণ এখানে ACID ট্রানজেকশন লাগে)। কিন্তু ইউজার যখন প্রোডাক্ট সার্চ করে (Read), তখন সেটি আসে Elasticsearch বা Redis থেকে (কারণ SQL-এর `LIKE %..%` কোয়েরি লাখ লাখ ডেটায় অনেক স্লো)।

**ডেটা সিঙ্ক (Sync) কীভাবে করা হয়? (৩টি উপায়):**

1. **The Outbox Pattern + Message Broker (RabbitMQ / Kafka):**
   * **কিভাবে কাজ করে:** যখন আপনি PostgreSQL-এ ডেটা সেভ করেন, একই Transaction-এ আপনি আরেকটি টেবিল (যাকে Outbox বলা হয়)-এ একটি Event সেভ করে রাখেন (যেমন: `UserCreatedEvent`)। 
   * এরপর ব্যাকগ্রাউন্ডে একটি Worker Service (Hangfire/Quartz) Outbox টেবিল থেকে ডেটা পড়ে RabbitMQ বা Kafka-তে পাঠিয়ে দেয়। Read ডেটাবেসের সার্ভিস সেই ইভেন্ট শুনে নিজের Elasticsearch/MongoDB আপডেট করে নেয়।
   * **সুবিধা:** ডেটা হারানোর কোনো চান্স নেই, কারণ Write এবং Event সেভ হওয়া একই SQL Transaction-এর ভেতরে ঘটে।

2. **Change Data Capture (CDC) - Debezium:**
   * **কিভাবে কাজ করে:** এটি সবচেয়ে মডার্ন এবং ফাস্ট উপায়। PostgreSQL-এ একটি **WAL (Write-Ahead Log)** ফাইল থাকে। ডেটাবেসে কোনো পরিবর্তন হলে সেটা প্রথমে এই লগে লেখা হয়। 
   * Debezium (Kafka Connect-এর একটি টুল) সরাসরি এই লগ ফাইল মনিটর করে। যেইমাত্র লগে পরিবর্তন আসে, সে সাথে সাথে ইভেন্ট বানিয়ে Kafka-তে পাঠিয়ে দেয়।
   * **সুবিধা:** অ্যাপ্লিকেশন কোডে (C# / .NET) আপনাকে কোনো ইভেন্ট ফায়ার করার কোড লিখতে হয় না। এটি সরাসরি ডেটাবেস লেভেল থেকে কাজ করে।

3. **Logical Replication / Materialized Views (Simpler Approach):**
   * যদি আপনি আলাদা কোনো NoSQL ডেটাবেস (Elasticsearch) মেইনটেইন করার ঝামেলায় না যেতে চান, তবে PostgreSQL-এর নিজস্ব **Materialized View** ব্যবহার করতে পারেন।
   * অথবা, একটি Read-Replica (Master-Slave) ডেটাবেস বানিয়ে Read ট্রাফিক সেখানে পাঠাতে পারেন। এটি তুলনামূলক অনেক সহজ (Level 2 CQRS)।

### 🇬🇧 English

**Is this actually used in the real world?**
Yes, absolutely. This architecture is known as **Polyglot Persistence**. Tech giants (Netflix, Uber, Amazon) and large-scale E-commerce platforms use this heavily.
For example, in E-commerce, placing an order (Write) hits a strict Relational DB (PostgreSQL) for ACID guarantees. However, searching the product catalog (Read) hits Elasticsearch or Redis because SQL `LIKE` queries are too slow at scale.

**How is the data synchronized? (3 Methods):**

1. **The Outbox Pattern + Message Broker (RabbitMQ / Kafka):**
   * **How it works:** When you save the domain entity in PostgreSQL, you also save an Event (e.g., `UserCreatedEvent`) into a separate `Outbox` table within the **same SQL transaction**. 
   * A background worker then reads the `Outbox` table and publishes the event to RabbitMQ/Kafka. The Read Service consumes this event and updates Elasticsearch/NoSQL.
   * **Benefit:** Guarantees zero data loss because the entity save and the event save happen in a single transaction.

2. **Change Data Capture (CDC) - Debezium:**
   * **How it works:** This is the most modern, high-performance approach. PostgreSQL maintains a **WAL (Write-Ahead Log)**.
   * A tool like Debezium monitors this WAL at the database level. The millisecond a row is inserted/updated, Debezium streams that change directly into Kafka.
   * **Benefit:** Zero application code is required to publish events. The sync happens transparently at the database infrastructure level.

3. **Logical Replication / Materialized Views (The Simpler Approach):**
   * You can use **Materialized Views** to pre-compute and store complex read queries, or route all read traffic to a Read-Only Replica (Master-Slave architecture). This represents a simpler Level 2 CQRS.

---
<br>

## 8. Deep Dive: MediatR Pipeline Behaviors
<a id="8-deep-dive-mediatr-pipeline-behaviors"></a>

### 🇧🇩 বাংলা (Bengali)

**Pipeline Behavior কী? (What is it?)**
এন্টারপ্রাইজ অ্যাপ্লিকেশনে কিছু কাজ থাকে যা প্রত্যেকটি রিকোয়েস্টে করতে হয়, যেমন- Logging, Validation, Caching, বা Performance Tracking। এগুলোকে বলা হয় **Cross-Cutting Concerns**। 
যদি আমরা MediatR Pipeline Behavior ব্যবহার না করি, তবে প্রতিটি Command Handler-এর ভেতরে এই লজিকগুলো বারবার লিখতে হবে, যা DRY (Don't Repeat Yourself) প্রিন্সিপাল ব্রেক করবে। Pipeline Behavior হলো ASP.NET Core Middleware-এর মতো একটি কনসেপ্ট, যা MediatR রিকোয়েস্ট Handler-এ ঢোকার ঠিক আগে এবং পরে কাজ করে।

**Architecture & How it works (কিভাবে কাজ করে?):**
এটি মূলত **Decorator Pattern** এবং **Chain of Responsibility Pattern** এর উপর ভিত্তি করে কাজ করে।
`Client Request -> Logging Pipeline -> Validation Pipeline -> Performance Pipeline -> [Actual Handler] -> Return Response`

**Code Deep Dive (Validation Pipeline):**
Clean Architecture-এ সবচেয়ে বেশি ব্যবহৃত হয় `ValidationBehavior` (FluentValidation এর সাথে)। 
```csharp
public class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators) 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next(); // কোনো ভ্যালিডেটর না থাকলে পরের ধাপে চলে যাও
        }

        var context = new ValidationContext<TRequest>(request);

        // সব ভ্যালিডেটর একসাথে রান করা
        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
        {
            // ভ্যালিডেশন ফেইল করলে Handler-এ রিকোয়েস্ট না পাঠিয়ে এখানেই Exception থ্রো করো
            throw new ValidationException(failures);
        }

        // সব ঠিক থাকলে আসল Handler-কে কল করো
        return await next();
    }
}
```

**Setup in Program.cs (কিভাবে রেজিস্টার করবেন?):**
```csharp
builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssembly(typeof(ApplicationAssembly).Assembly);
    
    // Pipeline Behaviors Registration (Order matters!)
    config.AddOpenBehavior(typeof(LoggingBehavior<,>));
    config.AddOpenBehavior(typeof(ValidationBehavior<,>));
    config.AddOpenBehavior(typeof(PerformanceBehavior<,>));
});
```

**Benefits (সিনিয়র আর্কিটেক্টের দৃষ্টিতে সুবিধা):**
1. **DRY (Don't Repeat Yourself):** আপনার যদি ১০০টি Command থাকে, তাহলেও ভ্যালিডেশন বা লগিংয়ের কোড শুধু ১ জায়গাতেই (Pipeline-এ) লিখতে হবে।
2. **SRP (Single Responsibility Principle):** Handler-এর কাজ শুধু বিজনেস লজিক এক্সিকিউট করা। সে জানেই না যে রিকোয়েস্টটি আগে ভ্যালিডেট বা লগ করা হয়েছে।
3. **Fail-Fast Mechanism:** রিকোয়েস্ট অবৈধ হলে তা ডেটাবেস পর্যন্ত পৌঁছানোর আগেই Pipeline থেকে রিজেক্ট হয়ে যায়, ফলে সার্ভার রিসোর্স বাঁচে।

**Trade-offs (খারাপ দিক):**
1. **Hidden Magic / Implicit Flow:** জুনিয়র ডেভেলপাররা অনেক সময় বুঝতে পারে না যে কখন এবং কিভাবে Validation বা Exception কাজ করছে, কারণ কোডটি সরাসরি Handler-এর ভেতরে দেখা যায় পণ্ডিত।
2. **Performance Overhead:** অতিরিক্ত Pipeline যুক্ত করলে মেমোরি অ্যালোকেশন বাড়ে। খুব হাই-পারফরম্যান্স সিস্টেমে এটি সামান্য ইমপ্যাক্ট ফেলতে পারে।

### 🇬🇧 English

**What is a Pipeline Behavior?**
In enterprise applications, tasks like Logging, Validation, Authorization, and Caching are needed for almost every request. These are known as **Cross-Cutting Concerns**. If we write this logic inside every Command Handler, we violate the DRY principle. MediatR Pipeline Behavior acts exactly like an ASP.NET Core Middleware, wrapping the execution of the request before and after the actual Handler is called.

**Architecture & How it works:**
It implements the **Chain of Responsibility** and **Decorator** patterns.
The execution flow looks like this:
`Client Request -> Logging Pipeline -> Validation Pipeline -> [Actual Command Handler] -> Return Response`

**Code Deep Dive (Validation Pipeline):**
(See the C# snippet in the Bengali section above). The behavior intercepts the request, runs all registered `FluentValidation` rules, and if any fail, it throws an Exception (or returns a failure Result object) *before* the Handler is ever executed. `await next()` is the mechanism that delegates execution to the next pipeline in the chain, or ultimately to the Handler.

**Benefits:**
1. **DRY Principle:** Write validation/logging logic once; apply it automatically to 100+ commands.
2. **Single Responsibility:** The Command Handler is stripped of noise. It only contains pure business/domain logic.
3. **Fail-Fast:** Invalid requests are rejected at the edge of the Application layer, saving database connections and compute resources.

**Trade-offs:**
2. **Performance Overhead:** The Chain of Responsibility pattern involves multiple delegate allocations. While negligible for most systems, it can add slight overhead in ultra-low latency scenarios.

---
<br>

## 9. In-Depth: Pipeline Flow & Design Patterns
<a id="9-in-depth-pipeline-flow--design-patterns"></a>

### 🇧🇩 বাংলা (Bengali)

**১. The Pipeline Flow Details (স্টেপ-বাই-স্টেপ ফ্লো):**
যখন ক্লায়েন্ট একটি রিকোয়েস্ট (যেমন: `SignupCommand`) পাঠায়, তখন সেটি সরাসরি Handler-এ যায় না। এটি ক্রমানুসারে নিচের ধাপগুলো পার করে:

*   **Step 1: Logging Pipeline:** রিকোয়েস্টটি প্রথমে এখানে ঢোকে। এই পাইপলাইন রিকোয়েস্টের নাম, ডেটা (পাসওয়ার্ড বাদে), এবং শুরুর সময় লগ করে। এরপর সে কল করে `await next();` (যার মানে "পরের পাইপলাইনে যাও")।
*   **Step 2: Validation Pipeline:** রিকোয়েস্ট এখানে আসার পর, এটি FluentValidation-এর সবগুলো রুল রান করে। 
    * যদি ফেইল করে: এখানেই Exception থ্রো করে। রিকোয়েস্ট আর সামনের দিকে এগোবে না (Fail-Fast)।
    * যদি পাস করে: সে কল করে `await next();`।
*   **Step 3: Performance Pipeline:** এটি মূলত একটি স্টপওয়াচ (Stopwatch) স্টার্ট করে। এরপর সে কল করে `await next();`।
*   **Step 4: Actual Handler:** অবশেষে রিকোয়েস্টটি `SignupCommandHandler`-এ পৌঁছায়। এখানে ইউজারের ডেটাবেস লজিক এক্সিকিউট হয় এবং একটি Response তৈরি হয়।
*   **Step 5: The Return Journey:** রেসপন্স তৈরি হওয়ার পর এটি আবার উল্টো পথে ফেরে। Performance Pipeline স্টপওয়াচ বন্ধ করে সময় মাপে (যদি ৫০০ms এর বেশি লাগে, তবে একটি Warning লগ করে)। এরপর Logging Pipeline রেসপন্সটি লগ করে ক্লায়েন্টের কাছে পাঠিয়ে দেয়।

**২. Decorator Pattern (ডেকোরেটর প্যাটার্ন):**
*   **কী?** ডেকোরেটর প্যাটার্ন হলো এমন একটি ডিজাইন প্যাটার্ন যেখানে কোনো অবজেক্টের মূল কোড পরিবর্তন না করেই, রানটাইমে (Run-time) তার সাথে নতুন কোনো ফিচার বা বিহেভিয়র (যেমন: লগিং) যুক্ত করা যায়।
*   **কিভাবে কাজ করে?** এটি মূল অবজেক্টটিকে একটি "Wrapper" ক্লাসের ভেতরে ঢুকিয়ে দেয়। 
*   **কোড এক্সাম্পল:**
    ```csharp
    // মূল ইন্টারফেস
    public interface IHandler { void Handle(); }
    
    // আসল ইমপ্লিমেন্টেশন
    public class ActualHandler : IHandler {
        public void Handle() => Console.WriteLine("Business Logic executing...");
    }
    
    // ডেকোরেটর (Wrapper)
    public class LoggingDecorator : IHandler {
        private readonly IHandler _innerHandler;
        
        // কন্সট্রাক্টরের মাধ্যমে আসল অবজেক্ট ইনজেক্ট করা হয়
        public LoggingDecorator(IHandler innerHandler) => _innerHandler = innerHandler;
        
        public void Handle() {
            Console.WriteLine("Start Logging..."); // নতুন ফিচার (Before)
            _innerHandler.Handle();                // আসল কাজ
            Console.WriteLine("End Logging...");   // নতুন ফিচার (After)
        }
    }
    ```
    **বাস্তব ব্যবহার:** MediatR-এর `IPipelineBehavior` মূলত এই ডেকোরেটর প্যাটার্ন ব্যবহার করেই আপনার Command Handler-কে চারপাশ থেকে র‍্যাপ (Wrap) করে রাখে।

**৩. Chain of Responsibility Pattern:**
*   **কী?** এই প্যাটার্নে একটি রিকোয়েস্ট একাধিক হ্যান্ডলারের একটি "চেইন" (Chain) এর মধ্য দিয়ে যায়। চেইনের প্রত্যেকটি হ্যান্ডলার সিদ্ধান্ত নেয় যে সে রিকোয়েস্টটি প্রসেস করবে কিনা, এবং রিকোয়েস্টটি চেইনের পরবর্তী হ্যান্ডলারের কাছে পাস করবে কিনা।
*   **কিভাবে কাজ করে?** `ASP.NET Core Middleware` এবং `MediatR Pipelines` হুবহু এই প্যাটার্ন ফলো করে।
*   **কোড এক্সাম্পল:**
    ```csharp
    public abstract class HandlerChain {
        protected HandlerChain _next;
        public void SetNext(HandlerChain next) => _next = next;
        public virtual void Process(Request req) => _next?.Process(req);
    }
    
    public class ValidationHandler : HandlerChain {
        public override void Process(Request req) {
            if (!req.IsValid) throw new Exception("Invalid"); // চেইন ব্রেক! (Fail-Fast)
            base.Process(req); // পরের হ্যান্ডলারে পাস করো (equivalent to 'await next()')
        }
    }
    ```
    **বাস্তব ব্যবহার:** MediatR-এ যখন আমরা `await next();` কল করি, সেটি মূলত এই প্যাটার্ন ফলো করে চেইনের পরবর্তী পাইপলাইন বা অরিজিনাল হ্যান্ডলারকে কল করে।

### 🇬🇧 English

**1. The Pipeline Flow Details:**
When a client sends a `SignupCommand`, it doesn't go straight to the Handler. It traverses a chain:
*   **Step 1: Logging Pipeline:** Logs the incoming request details, then calls `await next()`.
*   **Step 2: Validation Pipeline:** Runs validation rules. If invalid, it throws an exception and halts the chain (Fail-Fast). If valid, it calls `await next()`.
*   **Step 3: Performance Pipeline:** Starts a Stopwatch to track execution time, then calls `await next()`.
*   **Step 4: Actual Handler:** The core business logic executes and generates a Response.
*   **Step 5: Return Journey:** The Response bubbles back up the chain. The Performance pipeline stops the stopwatch (logging a warning if it took > 500ms). The Logging pipeline logs the final output and returns it to the client.

**2. Decorator Pattern:**
*   **Concept:** Allows you to attach new behaviors to an object at runtime without modifying its existing source code. It achieves this by wrapping the original object inside a Decorator class.
*   **How it relates:** MediatR Pipeline Behaviors act as Decorators. They wrap your Command Handlers, adding cross-cutting concerns (logging, validation) around the core execution (`_inner.Handle()`). (See C# snippet above).

**3. Chain of Responsibility Pattern:**
*   **How it relates:** When a MediatR Pipeline calls `await next()`, it is passing the request down the chain. If `ValidationBehavior` finds errors, it throws an exception instead of calling `next()`, effectively breaking the chain. This is identical to how ASP.NET Core Middlewares operate.

---
<br>

## 10. Architect Interview Prep: Top Questions & Answers
<a id="10-architect-interview-prep-top-questions--answers"></a>

### 🇧🇩 বাংলা (Bengali)

সফটওয়্যার আর্কিটেক্ট বা সিনিয়র লেভেলের ইন্টারভিউতে সাধারণত কোডিং সিনট্যাক্সের চেয়ে "Trade-offs" এবং "Design Choices" নিয়ে বেশি প্রশ্ন করা হয়। নিচে কিছু গুরুত্বপূর্ণ প্রশ্ন ও উত্তর দেওয়া হলো:

**Q1: আপনি কেন Clean Architecture ব্যবহার করছেন? সাধারণ N-Tier (3-Tier) আর্কিটেকচার কেন নয়?**
*   **Answer:** 3-Tier আর্কিটেকচারে বিজনেস লজিক অনেক সময় ডেটাবেস (Data Access Layer) বা ফ্রেমওয়ার্কের উপর ডিপেন্ডেন্ট হয়ে যায়। প্রজেক্ট বড় হলে এটি মেইনটেইন করা কঠিন। Clean Architecture-এ "Dependency Inversion" এর মাধ্যমে আমাদের কোর বিজনেস লজিককে (Domain Layer) সম্পূর্ণ স্বাধীন (Pure C#) রাখা যায়। তবে আমি এটাও বলবো যে, ছোট বা সাধারণ CRUD প্রজেক্টের জন্য Clean Architecture হলো "Over-engineering" কারণ এটি প্রচুর Boilerplate কোড তৈরি করে।

**Q2: CQRS কখন ব্যবহার করা উচিত নয়?**
*   **Answer:** যখন সিস্টেমে Read এবং Write এর রেশিও প্রায় সমান থাকে এবং বিজনেস লজিক খুব সিম্পল (শুধুই ডেটা ইনসার্ট আর সিলেক্ট) হয়, তখন CQRS ব্যবহার করা উচিত নয়। এটি অযথাই সিস্টেমের কমপ্লেক্সিটি বাড়িয়ে দেয়। 

**Q3: Eventual Consistency-তে "Ghost Read" বা "Stale Data" সমস্যাটি আপনি ফ্রন্টএন্ডে কিভাবে হ্যান্ডেল করবেন?**
*   **Answer:** যেহেতু Read Database সিঙ্ক হতে কিছুটা সময় নেয়, তাই আমি **Optimistic UI Update** টেকনিক ব্যবহার করবো। অর্থাৎ, সার্ভার থেকে রেসপন্স আসার আগেই UI-তে নতুন ডেটা দেখিয়ে দেবো। পাশাপাশি **RYOW (Read-Your-Own-Writes)** মেকানিজম ব্যবহার করবো, যেখানে ইউজারের লেখা ডেটাটি তার লোকাল সেশন বা ক্যাশে সেভ থাকবে এবং পরের রিডে ডেটাবেসের বদলে ক্যাশ থেকে ডেটা সার্ভ করা হবে।

**Q4: MediatR Pipeline Behavior ব্যবহার না করে আমরা কি সরাসরি Handler-এর ভেতর Validation বা Logging লিখতে পারতাম না? এতে সমস্যা কী ছিল?**
*   **Answer:** হ্যাঁ পারতাম। কিন্তু এতে দুটি বড় সমস্যা হতো। প্রথমত, **DRY (Don't Repeat Yourself)** প্রিন্সিপাল ব্রেক হতো; ১০০টি কমান্ড থাকলে ১০০ জায়গায় একই কোড লিখতে হতো। দ্বিতীয়ত, **SRP (Single Responsibility Principle)** ব্রেক হতো; Handler-এর কাজ শুধু বিজনেস লজিক এক্সিকিউট করা, ভ্যালিডেশন বা লগিং করা তার দায়িত্ব নয়। Pipeline Behavior মূলত **Decorator Pattern** ব্যবহার করে এই Cross-cutting concerns গুলোকে মূল লজিক থেকে আলাদা করে দেয়।

**Q5: Polyglot Persistence (SQL + NoSQL) এর ডেটা সিঙ্ক করার সবচেয়ে রিলায়েবল পদ্ধতি কোনটি?**
*   **Answer:** সবচেয়ে রিলায়েবল পদ্ধতি হলো **The Outbox Pattern**। ডেটাবেসে মূল Entity সেভ করার সময় একই SQL Transaction-এর ভেতর আমরা একটি Event বা মেসেজ `Outbox` টেবিলে সেভ করি। এতে ডেটা সেভ হওয়া এবং ইভেন্ট জেনারেট হওয়া ১০০% গ্যারান্টিড থাকে (Zero Data Loss)। এরপর একটি ব্যাকগ্রাউন্ড ওয়ার্কার সেই ইভেন্টটি Kafka বা RabbitMQ-তে পাবলিশ করে, যা Read Database কনজিউম করে।

### 🇬🇧 English

In Software Architect or Senior Engineer interviews, interviewers focus more on "Trade-offs" and "Design Choices" rather than raw syntax. Here are some of the most critical questions:

**Q1: Why choose Clean Architecture over a standard N-Tier (3-Tier) architecture?**
*   **Answer:** In N-Tier architectures, the business logic often becomes tightly coupled with the database or UI framework. Clean Architecture uses "Dependency Inversion" to keep the Domain Layer pure and independent of external tools. However, I always mention the trade-off: for simple CRUD applications, Clean Architecture is an "Over-engineering" mistake that introduces too much boilerplate code.

**Q2: When should you NOT use CQRS?**
*   **Answer:** You should not use CQRS when the application is mostly simple CRUD operations with a balanced read/write ratio. Introducing CQRS in such scenarios unnecessarily skyrockets the architectural complexity and maintenance cost without providing tangible benefits.

**Q3: How do you handle "Ghost Reads" or "Stale Data" on the frontend caused by Eventual Consistency?**
*   **Answer:** Because the Read Database takes time to sync, I would implement **Optimistic UI Updates**, where the UI reflects the change immediately without waiting for the backend. Additionally, I would use the **RYOW (Read-Your-Own-Writes)** pattern, caching the user's write locally or in Redis, so their immediate subsequent reads pull from the cache instead of the potentially stale Read Database.

**Q4: Instead of MediatR Pipeline Behaviors, couldn't we just write Validation and Logging directly inside the Handlers? What's the problem?**
*   **Answer:** Yes, but it causes two major issues. First, it violates the **DRY (Don't Repeat Yourself)** principle; we'd duplicate logging code across 100+ handlers. Second, it violates the **SRP (Single Responsibility Principle)**; a Handler should only orchestrate business logic, not worry about HTTP validation. Pipeline Behaviors use the **Decorator Pattern** to cleanly separate these cross-cutting concerns from the core logic.

**Q5: What is the most reliable way to sync data in Polyglot Persistence (e.g., PostgreSQL to Elasticsearch)?**
*   **Answer:** The most reliable method is the **Outbox Pattern**. We save the domain entity and an Event (e.g., `UserCreated`) into an `Outbox` table within the exact same ACID SQL Transaction. This guarantees zero data loss. A background worker then polls this table and safely publishes the events to a message broker like Kafka or RabbitMQ for the Read Database to consume.

---
<br>

## 11. FluentValidation in Clean Architecture
<a id="11-fluentvalidation-in-clean-architecture"></a>

### 🇧🇩 বাংলা (Bengali)

**FluentValidation কী?**
FluentValidation হলো .NET-এর জন্য একটি জনপ্রিয় লাইব্রেরি যা দিয়ে অবজেক্ট ভ্যালিডেশনের রুলস তৈরি করা হয়। এটি "Fluent Interface" বা মেথড চেইনিং (যেমন: `RuleFor(x => x.Name).NotEmpty().MaximumLength(50)`) ব্যবহার করে, যা পড়তে একদম ইংরেজির মতো লাগে।

**Data Annotations বনাম FluentValidation:**
সাধারণত আমরা ক্লাসের প্রপার্টির উপরে `[Required]`, `[EmailAddress]` লিখে ভ্যালিডেট করি (যাকে Data Annotations বলে)। কিন্তু Clean Architecture-এ এটি কড়াভাবে নিষিদ্ধ। কারণ:
1. **Coupling:** Data Annotations আপনার মূল ডোমেইন মডেল বা Command/Query ক্লাসকে ভ্যালিডেশন লজিকের সাথে যুক্ত (Couple) করে ফেলে। 
2. **SRP Violation:** একটি ক্লাসের দায়িত্ব শুধু ডেটা হোল্ড করা, নিজেকে নিজে ভ্যালিডেট করা নয়।
FluentValidation এই সমস্যা দূর করে। এটি ভ্যালিডেশন রুলসকে একটি সম্পূর্ণ আলাদা ক্লাসে (Validator Class) রাখে।

**কোড এক্সাম্পল (আমাদের প্রজেক্টের SignupCommand):**
ধরা যাক, আমাদের একটি `SignupCommand` আছে:
```csharp
public record SignupCommand(
    string FullName, 
    string Email, 
    string Password, 
    string Role) : IRequest<AuthResult>;
```
এর জন্য FluentValidation ক্লাসটি হবে একদম আলাদা:
```csharp
public class SignupCommandValidator : AbstractValidator<SignupCommand>
{
    public SignupCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full Name is required.")
            .MaximumLength(100).WithMessage("Full Name cannot exceed 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email is required.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a number.");
            
        // Conditional Validation (জটিল লজিক)
        RuleFor(x => x.Role)
            .Must(BeAValidRole).WithMessage("Role must be Admin, Agent, or Client.")
            .When(x => !string.IsNullOrEmpty(x.Role));
    }

    private bool BeAValidRole(string role)
    {
        var validRoles = new[] { "Admin", "Agent", "Client" };
        return validRoles.Contains(role);
    }
}
```

**কিভাবে এটি MediatR-এর সাথে কাজ করে?**
আগের সেকশনে (Section 8) আমরা যে `ValidationBehavior` বানিয়েছিলাম, সেটি ব্যাকগ্রাউন্ডে Dependency Injection (DI) কন্টেইনার থেকে এই `SignupCommandValidator` কে খুঁজে বের করে। যখনই `SignupCommand` রান হবে, সে নিজে নিজেই এই ভ্যালিডেটরটি রান করবে এবং যদি কোনো রুল ফেইল করে, তবে সে সাথে সাথে Exception থ্রো করবে। 

**Behind the Scenes (ব্যাকগ্রাউন্ডে এটি কীভাবে কাজ করে?):**
1. **Expression Trees:** যখন আমরা `RuleFor(x => x.Email)` লিখি, তখন C#-এর `Expression Trees` ব্যবহার করে রানটাইমে প্রপার্টির নাম এবং ভ্যালু বের করে আনা হয়। এর ফলে হার্ডকোডেড স্ট্রিং (যেমন: "Email") লিখতে হয় না এবং Refactoring অনেক সহজ হয়।
2. **Rule Aggregation:** `AbstractValidator` ইন্টারনালি একটি লিস্ট মেইনটেইন করে। `.NotEmpty()`, `.EmailAddress()`—এগুলো একেকটি `IPropertyValidator` তৈরি করে সেই লিস্টে জমা করে।
3. **DI Container (Reflection):** আপনি যখন `Program.cs`-এ `AddValidatorsFromAssembly()` রান করেন, তখন .NET Reflection ব্যবহার করে পুরো প্রজেক্ট স্ক্যান করে দেখে কোথায় কোথায় `AbstractValidator<T>` আছে। এরপর সেগুলোকে `IValidator<T>` হিসেবে DI Container-এ রেজিস্টার করে ফেলে। 
4. **Execution:** MediatR `ValidationBehavior` রান হওয়ার সময় DI Container থেকে সবগুলো `IValidator` কল করে এবং `Task.WhenAll` দিয়ে প্যারালালি (একসাথে) সবগুলো ভ্যালিডেশন চেক করে।

### 🇬🇧 English

**What is FluentValidation?**
FluentValidation is a popular .NET library used for building strongly-typed validation rules. It uses a fluent interface (method chaining), making the validation logic highly readable and expressive.

**Data Annotations vs. FluentValidation:**
In traditional apps, developers use Data Annotations (like `[Required]`) directly on properties. In Clean Architecture, this is strongly discouraged because:
1. **Tight Coupling:** It couples your clean Domain/Application models with UI/Framework-level validation logic.
2. **SRP Violation:** A model should represent data, not hold the logic to validate itself.
FluentValidation solves this by moving all validation logic into a separate, dedicated Validator class.

**Code Example:**
(See the C# snippet in the Bengali section above). Notice how complex validation logic (like Regex matching for passwords or conditional validation based on other properties) can be easily written inside the constructor of the `AbstractValidator`.

**Integration with MediatR:**
As discussed in Section 8, FluentValidation pairs perfectly with MediatR's `IPipelineBehavior`. The `ValidationBehavior` intercepts the MediatR request, automatically resolves the corresponding FluentValidation class from the DI container, runs the rules, and throws an exception if validation fails—all before the Command Handler is even invoked. This keeps your Handlers 100% focused on business logic.

**Behind the Scenes (How does it work?):**
1. **Expression Trees:** `RuleFor(x => x.Email)` utilizes C# Expression Trees to dynamically extract the property name and value at runtime, providing type safety and refactoring support without magic strings.
2. **Rule Aggregation:** The `AbstractValidator` maintains an internal collection of rules. Chained methods like `.NotEmpty()` add specific `IPropertyValidator` instances to this collection.
**Execution:** The MediatR `ValidationBehavior` injects an `IEnumerable<IValidator<TRequest>>`, executes all validators concurrently via `Task.WhenAll`, and aggregates any failures before proceeding.

---
<br>

## 12. Case Study: NexConvo Identity Service Architecture
<a id="12-case-study-nexconvo-identity-service-architecture"></a>

### 🇧🇩 বাংলা (Bengali)

আমাদের প্রজেক্টের (NexConvo) `Identity` সার্ভিসের সোর্স কোড (বিশেষ করে `Signup.cs`) স্টাডি করলে বেশ কিছু দুর্দান্ত রিয়েল-ওয়ার্ল্ড আর্কিটেকচারাল ডিসিশন বা ডিজাইন চয়েস দেখা যায়। নিচে সেগুলো নিয়ে বিস্তারিত আলোচনা করা হলো:

**১. Vertical Slice Architecture (Feature-based grouping):**
*   **কী করা হয়েছে?** আপনি যদি `Signup.cs` ফাইলটি দেখেন, দেখবেন `SignupCommand`, `SignupCommandValidator` এবং `SignupCommandHandler`—তিনটি ক্লাসই একটিমাত্র ফাইলের ভেতরে লেখা হয়েছে।
*   **কেন করা হয়েছে?** সাধারণত আমরা `Commands` ফোল্ডারে কমান্ড, `Validators` ফোল্ডারে ভ্যালিডেটর রাখি। কিন্তু এতে কোড নেভিগেট করা কঠিন হয়। Vertical Slice Architecture অনুযায়ী একটি ফিচারের (যেমন: Signup) সব কোড এক জায়গায় রাখলে "Cohesion" (পারস্পরিক সম্পর্ক) বাড়ে এবং মেইনটেইন করা সহজ হয়।

**২. ASP.NET Core Identity এর বদলে Custom PBKDF2 Hashing:**
*   **কী করা হয়েছে?** প্রজেক্টে মাইক্রোসফটের ডিফল্ট `ASP.NET Core Identity` ফ্রেমওয়ার্ক ব্যবহার না করে, কাস্টম `IPasswordHasher` (যেটা ব্যাকগ্রাউন্ডে `Pbkdf2PasswordHasher` ব্যবহার করে) বানানো হয়েছে।
*   **কেন করা হয়েছে (Alternative & Trade-off)?** ASP.NET Core Identity অনেক পাওয়ারফুল, কিন্তু এটি সাথে করে অনেকগুলো ডিফল্ট টেবিল (`AspNetUsers`, `AspNetRoles`, `AspNetUserClaims`) নিয়ে আসে যা আমাদের ডাটাবেস স্কিমাকে ভারি করে তোলে। যেহেতু আমাদের সিস্টেমে **Row-Level Security (RLS)** এবং Multi-tenancy আছে, তাই মাইক্রোসফটের ডিফল্ট টেবিলের বদলে নিজেদের মতো করে কাস্টম এবং হালকা (Lightweight) Identity সিস্টেম বানানোই বেশি লজিক্যাল ছিল।

**৩. Multi-Tenancy (RLS) Context Injection:**
*   **কী করা হয়েছে?** `SignupCommandHandler.cs`-এ নতুন টেন্যান্ট তৈরি করার পর `tenantSetter.SetTenant(tenant.Id)` কল করা হয়েছে।
*   **কেন করা হয়েছে?** যেহেতু ডেটাবেসে Row-Level Security (RLS) এনাবল করা আছে, তাই `SetTenant` কল না করে ইউজারের ডেটা ইনসার্ট করতে গেলে ডেটাবেস থেকে `Permission Denied` এরর আসবে। এটি আমাদের আর্কিটেকচারের সবচেয়ে সিকিউর একটি অংশ—যাতে এক কোম্পানির ডেটা ভুলে অন্য কোম্পানির ড্যাশবোর্ডে চলে না যায়।

**৪. The "Best-Effort" Email Delivery Pattern:**
*   **কী করা হয়েছে?** `TrySendVerification` মেথডে ইমেইল পাঠানোর কোডটিকে একটি `try-catch` ব্লকের ভেতর রাখা হয়েছে এবং কোনো `Exception` থ্রো করা হয়নি (Swallow করা হয়েছে)।
*   **কেন করা হয়েছে (Alternative)?** যদি ইমেইল প্রোভাইডার (যেমন- SendGrid) ডাউন থাকে, তবে ইমেইল সেন্ড ফেইল করবে। এখন ইমেইলের জন্য যদি পুরো ট্রানজেকশন ফেইল করে ইউজার ক্রিয়েট হওয়া বন্ধ হয়ে যায়, সেটি খুবই বাজে UX (User Experience)। তাই একে "Best-Effort" হিসেবে রাখা হয়েছে। 
*   **বেটার অল্টারনেটিভ কী ছিল?** এর সবচেয়ে পারফেক্ট সলিউশন হলো **The Outbox Pattern + RabbitMQ** ব্যবহার করা। কিন্তু V1 (ভার্সন ১) বা MVP-এর জন্য এটি ওভার-ইঞ্জিনিয়ারিং হতে পারে ভেবেই সিম্পল `try-catch` ব্যবহার করা হয়েছে।

**৫. Rich Domain Model (DDD):**
*   **কী করা হয়েছে?** রিফ্রেশ টোকেন তৈরির সময় `DomainRefreshToken.Issue(...)` কল করা হয়েছে।
*   **কেন করা হয়েছে?** এটি Domain-Driven Design (DDD) এর একটি দারুণ উদাহরণ। টোকেন জেনারেট করার লজিক বা ভ্যালিডেশনগুলো ডোমেইন এন্টিটির ভেতরেই রাখা হয়েছে। এর ফলে ডেটাবেস মডেল শুধু "dumb data holder" না হয়ে বিজনেস রুলসগুলো নিজের ভেতরেই ইনক্যাপসুলেট (Encapsulate) করে রাখতে পারে।

### 🇬🇧 English

Studying the `NexConvo.Identity` service (specifically `Signup.cs`) reveals several senior-level architectural decisions and trade-offs. Here is a breakdown:

**1. Vertical Slice Architecture:**
*   **What was done:** The `SignupCommand`, `SignupCommandValidator`, and `SignupCommandHandler` are all grouped inside a single `Signup.cs` file.
*   **Why:** Instead of grouping files by technical concern (e.g., placing all handlers in a `Handlers` folder), grouping by Feature (Vertical Slicing) drastically improves cohesion and makes the codebase easier to navigate and maintain.

**2. Custom PBKDF2 Password Hashing instead of ASP.NET Core Identity:**
*   **What was done:** We skipped the default ASP.NET Core Identity framework and injected a custom `IPasswordHasher`.
*   **Why (Trade-off):** ASP.NET Core Identity is bloated. It forces schemas like `AspNetUsers` and `AspNetRoles`. Because NexConvo heavily relies on PostgreSQL **Row-Level Security (RLS)** for multi-tenancy, bringing in bloated default schemas would make RLS implementation messy. A lightweight, custom PBKDF2 solution fits perfectly.

**3. Multi-Tenancy (RLS) Context Injection:**
*   **What was done:** Before saving the new user, `tenantSetter.SetTenant(tenant.Id)` is explicitly called.
*   **Why:** PostgreSQL RLS strictly blocks any inserts/selects if the database session doesn't have the current `tenant_id` context. This ensures 100% data isolation at the database engine level.

*   **The Alternative:** A more robust (but complex) alternative would be using the **Outbox Pattern + RabbitMQ**, ensuring the email intent is saved in the same database transaction and sent reliably in the background. For MVP velocity, a simple `try-catch` is an acceptable trade-off.

---
<br>

## 13. Multi-Tenancy & Row-Level Security (RLS)
<a id="13-multi-tenancy--row-level-security-rls"></a>

### 🇧🇩 বাংলা (Bengali)

Multi-Tenancy এবং RLS (Row-Level Security) হলো এন্টারপ্রাইজ SaaS প্রজেক্টের সবচেয়ে গুরুত্বপূর্ণ আর্কিটেকচারাল ডিসিশন। 

**মাল্টি-ট্যানেন্সি (Multi-Tenancy) কী?**
SaaS সিস্টেমে (যেমন: Slack, Salesforce, NexConvo) অনেকগুলো ভিন্ন ভিন্ন কোম্পানি (Tenant) একসাথে একই অ্যাপ্লিকেশন ব্যবহার করে। ডেটা রাখার ক্ষেত্রে সবচেয়ে স্কেলেবল অ্যাপ্রোচ হলো **Shared Database, Shared Schema**। অর্থাৎ সবার ডেটা একটাই ডেটাবেসের একই টেবিলে থাকবে, কিন্তু প্রত্যেকটি টেবিলে একটি `TenantId` কলাম থাকবে।

**সমস্যা (Data Leak):** 
ডেভেলপার যদি C# কোডে `.Where(x => x.TenantId == currentTenantId)` লিখতে ভুলে যায়, তবে এক কোম্পানির ডেটা অন্য কোম্পানির ড্যাশবোর্ডে চলে যাবে। এটি একটি ভয়াবহ সিকিউরিটি ফ্ল।

**Row-Level Security (RLS) এর কাজ কী?**
এই হিউম্যান এরর ঠেকানোর জন্যই আমরা সরাসরি **ডেটাবেস ইঞ্জিন (PostgreSQL) লেভেলে** সিকিউরিটি বসিয়ে দেই। RLS চালু থাকলে ডেটাবেসকে বলে দেওয়া যায়— "কেউ যদি ডেটা চায়, আগে চেক করবি কানেকশনের সেশনে `tenant_id` কত সেট করা আছে। শুধু ওই নির্দিষ্ট `tenant_id`-এর ডেটাই দিবি।"
ফলে কোডে `.Where()` লিখতে ভুলে গেলেও ডেটাবেস নিজেই অন্য কোম্পানির ডেটা ফিল্টার আউট করে দেয়।

**কিভাবে ইমপ্লিমেন্ট করা হয়?**
১. **ডেটাবেস লেভেলে পলিসি তৈরি:**
```sql
ALTER TABLE "Users" ENABLE ROW LEVEL SECURITY;
CREATE POLICY "TenantIsolationPolicy" ON "Users"
    USING ("TenantId" = current_setting('app.current_tenant_id')::uuid);
```
২. **C#-এ ইন্টারসেপ্টর (Interceptor):**
যখন ক্লায়েন্ট রিকোয়েস্ট পাঠায়, তার টোকেনে থাকা TenantId-টি ধরে ডেটাবেস কানেকশন ওপেন করার সময় একটি SQL রান করা হয়:
```csharp
using var command = connection.CreateCommand();
command.CommandText = $"SET LOCAL app.current_tenant_id = '{tenantId}'";
await command.ExecuteNonQueryAsync();
```

### 🇬🇧 English

**Multi-Tenancy & RLS:**
In SaaS applications, multi-tenancy means multiple organizations (tenants) share the same underlying infrastructure. The most scalable approach is the **Shared Database, Shared Schema** model, where all tenants share the same tables, distinguished by a `TenantId` column.

**The Data Leak Problem:**
If a developer forgets to append `.Where(x => x.TenantId == currentTenantId)` in Entity Framework, cross-tenant data leakage occurs, which is a catastrophic security vulnerability.

**Row-Level Security (RLS) as the Solution:**
Instead of relying on application-level filtering, PostgreSQL's built-in RLS enforces data isolation directly at the database engine level. A policy is created in SQL that restricts all `SELECT`, `INSERT`, `UPDATE`, and `DELETE` operations strictly to rows where the `TenantId` matches the session's current `tenant_id`. 
Even if the C# code issues a raw `SELECT * FROM Users`, PostgreSQL intercepts it and only returns rows for the authenticated tenant.

---
<br>

## 14. Custom Password Hashing (PBKDF2) vs ASP.NET Core Identity
<a id="14-custom-password-hashing-pbkdf2-vs-aspnet-core-identity"></a>

### 🇧🇩 বাংলা (Bengali)

**ASP.NET Core Identity-এর সমস্যা কী? (The Bloat Problem)**
মাইক্রোসফটের `ASP.NET Core Identity` অত্যন্ত পাওয়ারফুল, কিন্তু এটি আপনার ডেটাবেসে বাই-ডিফল্ট অনেকগুলো টেবিল (`AspNetUsers`, `AspNetRoles`, `AspNetUserClaims`) তৈরি করে ফেলে। 
আমাদের SaaS প্রজেক্টে ডেটাবেস ডিজাইন হতে হবে একদম লিন (Lean) এবং ফোকাসড। যেহেতু আমাদের সিস্টেমে **Row-Level Security (RLS)** আছে, তাই মাইক্রোসফটের ওই ভারি টেবিলগুলোর ভেতরে আমাদের কাস্টম `TenantId` ঢোকাতে গেলে পুরো স্কিমা মেসি (Messy) হয়ে যায় এবং কোড ফ্রেমওয়ার্ক-ডিপেন্ডেন্ট হয়ে পড়ে (যা Clean Architecture-এর পরিপন্থী)।

**Custom Hashing (PBKDF2) এর আর্কিটেকচার:**
মাইক্রোসফটের ম্যাজিক বাদ দিয়ে আমরা সম্পূর্ণ নিজেদের কন্ট্রোলে একটি `Users` টেবিল তৈরি করেছি। পাসওয়ার্ড সিকিউর রাখার জন্য আমরা **PBKDF2 (Password-Based Key Derivation Function 2)** ব্যবহার করেছি। এটি পাসওয়ার্ডের সাথে একটি রেন্ডম `Salt` মিক্স করে এবং সেটিকে হাজার হাজার বার হ্যাশ করে। ফলে হ্যাকার ডেটাবেস চুরি করলেও পাসওয়ার্ড ডিক্রিপ্ট করতে পারে না।

**Infrastructure লেয়ারে ইমপ্লিমেন্টেশন:**
```csharp
// Domain Layer - ইন্টারফেস 
public interface IPasswordHasher 
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

// Infrastructure Layer - PBKDF2 ইমপ্লিমেন্টেশন
public class Pbkdf2PasswordHasher : IPasswordHasher
{
    public string Hash(string password) { /* PBKDF2 Hashing Logic */ }
    public bool Verify(string password, string hash) { /* Verification Logic */ }
}
```
**ট্রেড-অফ রেজাল্ট:** আমরা মাইক্রোসফটের আউট-অফ-দ্য-বক্স ফিচারগুলো স্যাক্রিফাইস করেছি, কিন্তু বিনিময়ে পেয়েছি ১০০% কাস্টমাইজেবল একটি লিন ডেটাবেস যা RLS এবং মাল্টি-ট্যানেন্সির জন্য পারফেক্ট।

### 🇬🇧 English

**The Bloat of ASP.NET Core Identity:**
While Microsoft's default `ASP.NET Core Identity` is powerful, it mandates a highly opinionated and bloated database schema (`AspNetUsers`, `AspNetRoles`, etc.). In a true SaaS architecture where we strictly rely on **Row-Level Security (RLS)**, hacking the default Identity schema to enforce multi-tenancy constraints becomes extremely messy and couples the Domain layer to the framework, violating Clean Architecture.

**The Custom PBKDF2 Solution:**
To maintain 100% control over our database schema, we designed a lightweight `Users` table and implemented a custom `IPasswordHasher` using **PBKDF2**. This algorithm mixes the password with a cryptographic salt and hashes it thousands of times, making brute-force attacks computationally infeasible.

**Trade-off Result:**
By avoiding the "magic" of ASP.NET Core Identity, we sacrificed out-of-the-box convenience but gained a lean, framework-agnostic Domain model that natively supports PostgreSQL RLS multi-tenancy.

---
<br>

## 15. Rich Domain Model (DDD) vs Anemic Domain Model
<a id="15-rich-domain-model-ddd-vs-anemic-domain-model"></a>

### 🇧🇩 বাংলা (Bengali)

আমাদের প্রজেক্টে টোকেন জেনারেট করার জন্য আমরা লজিকগুলো সার্ভিসের ভেতরে না লিখে সরাসরি ডেটাবেস এন্টিটি (Entity) বা মডেলের ভেতরে লিখেছি: `DomainRefreshToken.Issue(...)`। এটি Domain-Driven Design (DDD)-এর একটি কোর কনসেপ্ট।

**Anemic Domain Model (যেটা আমরা এড়িয়ে চলেছি):**
সাধারণত ডেভেলপাররা এন্টিটিকে একটি "Dumb Data Bag" হিসেবে ব্যবহার করে। অর্থাৎ, এর সব প্রপার্টির `get` এবং `set` ওপেন করা থাকে। এর ফলে অ্যাপ্লিকেশন লেয়ারের যেকোনো জায়গা থেকে ভুল করে `token.IsRevoked = true` সেট করে ডেটাবেস সেভ করে দেওয়া যায়। এটি Encapsulation ব্রেক করে।

**Rich Domain Model (আমাদের প্রজেক্টের অ্যাপ্রোচ):**
DDD-এর মূল কথাই হলো— "যে ক্লাসের ডেটা, সেই ক্লাসের ভেতরেই তার বিজনেস রুলস থাকবে।" 
আমাদের `RefreshToken` এন্টিটিতে সব প্রপার্টির setter প্রাইভেট (`private set;`) করা আছে। বাইরে থেকে কেউ ভ্যালু বসাতে পারবে না।

```csharp
public class RefreshToken
{
    public Guid TenantId { get; private set; }
    public bool IsRevoked { get; private set; }
    
    // টোকেন তৈরি করার একমাত্র রাস্তা 
    public static RefreshToken Issue(Guid tenantId, Guid userId, string hash, DateTime expires)
    {
        if (expires <= DateTime.UtcNow) throw new DomainException("Expiration must be in future.");
        return new RefreshToken { TenantId = tenantId, IsRevoked = false };
    }

    // টোকেন বাতিল করার জন্য
    public void Revoke() { IsRevoked = true; }
}
```
**সুবিধা:**
১. **Security:** বাইরে থেকে `token.IsRevoked = true` লেখা অসম্ভব। `Revoke()` মেথড কল করতে হবে।
২. **Centralized Logic:** টোকেন ভ্যালিডেশনের সব কোড এন্টিটির ভেতরেই থাকে, Handler বা Service ক্লাসের কোনো মাথাব্যথা থাকে না।

### 🇬🇧 English

**Rich Domain Model vs Anemic Domain Model:**
An "Anemic Domain Model" uses entities merely as data bags with public getters and setters, stripping them of business logic. This violates encapsulation, as invalid states can easily be saved to the database.

In our project, we follow Domain-Driven Design (DDD) principles to create a **Rich Domain Model**. For example, the `RefreshToken` entity has `private` setters. State transitions are strictly controlled through business methods like `DomainRefreshToken.Issue(...)` and `Revoke()`. This guarantees that the entity always remains in a valid state and centralizes business rules where they belong—inside the entity itself.

---
<br>

## 16. JWT & Refresh Token Architecture
<a id="16-jwt--refresh-token-architecture"></a>

### 🇧🇩 বাংলা (Bengali)

**JWT (Access Token) বনাম Refresh Token:**
*   **Access Token (VIP Pass):** এটি Stateless। সার্ভার ডেটাবেস চেক করে না, শুধু ক্রিপ্টোগ্রাফিক সিগনেচার ভেরিফাই করে। এটি খুব ফাস্ট, কিন্তু এর সবচেয়ে বড় দুর্বলতা হলো একে মাঝপথে বাতিল (Revoke) করা যায় না। তাই হ্যাকিং থেকে বাঁচতে এর মেয়াদ খুব ছোট (৫-১৫ মিনিট) রাখা হয়।
*   **Refresh Token (Master Key):** এটি Stateful (ডেটাবেসে সেভ থাকে)। যখন Access Token এক্সপায়ার হয়ে যায়, তখন এই চাবিটি সার্ভারকে দেখিয়ে নতুন একটি Access Token আনা হয়। এটি ডেটাবেস থেকে যেকোনো সময় Revoke করা যায়।

**The Core Architecture Flow:**
1. **Login:** ইউজার লগিন করলে সার্ভার একটি ১৫ মিনিটের Access Token এবং একটি ৭ দিনের Refresh Token তৈরি করে। রিফ্রেশ টোকেনটিকে হ্যাশ (Hash) করে ডেটাবেসে সেভ রাখে।
2. **API Calls:** ক্লায়েন্ট সব API কলে Access Token পাঠায়। সার্ভার শুধু সিগনেচার চেক করে ডেটা দেয় (No DB Call)।
3. **Refresh Magic:** ১৫ মিনিট পর Access Token এক্সপায়ার হলে, ক্লায়েন্ট ব্যাকগ্রাউন্ডে তার Refresh Token টি `/refresh` এন্ডপয়েন্টে পাঠায়। সার্ভার ডেটাবেস চেক করে (বাতিল কিনা) এবং নতুন একজোড়া Access ও Refresh Token ফেরত দেয়।

**Security Best Practices:**
*   **Refresh Token Rotation:** হ্যাকার টোকেন চুরি করলেও বাঁচার উপায় হলো Rotation। আমাদের সিস্টেমে রিফ্রেশ টোকেন একবার ব্যবহার করলেই তা বাতিল করে নতুন আরেকটি রিফ্রেশ টোকেন দেওয়া হয়। হ্যাকার যদি চুরি করা টোকেন ইউজ করতে যায়, সার্ভার পুরোনো টোকেনটিকে ইনভ্যালিড করে দেয় এবং আসল ইউজারকে লগ-আউট করে দেয়।
*   **HttpOnly Cookie:** XSS অ্যাটাক থেকে বাঁচতে Refresh Token-কে কখনোই LocalStorage-এ রাখা উচিত নয়, বরং `HttpOnly` কুকি হিসেবে সেভ রাখা আর্কিটেকচারাল স্ট্যান্ডার্ড।

### 🇬🇧 English

**JWT & Refresh Token Architecture:**
*   **Access Token (JWT):** A stateless, short-lived token (5-15 mins). Because it's stateless, the server verifies its cryptographic signature without hitting the database, making it extremely fast. However, it cannot be revoked instantly.
*   **Refresh Token:** A stateful, long-lived token (e.g., 7 days) stored securely in the database. Its sole purpose is to acquire a new Access Token once the current one expires. It can be revoked instantly.

**Security Mechanics (Behind the Scenes):**
1. **Refresh Token Rotation:** To prevent replay attacks if a token is stolen, we implement rotation. The moment a Refresh Token is used, it is revoked and a fresh token is issued.
2. **Database Hashing:** Refresh Tokens are never stored in plain-text. They are hashed in the database exactly like passwords.
3. **Storage Strategy:** To protect against XSS (Cross-Site Scripting) attacks, Access Tokens are typically kept in memory (e.g., React state), while Refresh Tokens are stored in strictly `HttpOnly` cookies that JavaScript cannot access.

---
<br>

## 17. The "Best-Effort" Delivery vs The Outbox Pattern
<a id="17-the-best-effort-delivery-vs-the-outbox-pattern"></a>

### 🇧🇩 বাংলা (Bengali)

**The Problem:**
ধরুন ইউজার সাইন-আপ করলো এবং আমরা তাকে ভেরিফিকেশন ইমেইল পাঠাবো। যদি আমরা `try-catch` ছাড়া ইমেইল সেন্ড করি এবং থার্ড-পার্টি API (যেমন: SendGrid) ডাউন থাকে, তবে Exception থ্রো হবে। ফলে ডেটাবেসের ট্রানজেকশন রোলব্যাক হয়ে যাবে এবং ইউজারের অ্যাকাউন্টটিই তৈরি হবে না! এটি খুবই বাজে UX।

**The "Best-Effort" Pattern (আমাদের বর্তমান সলিউশন):**
এই সমস্যা এড়াতে আমরা ইমেইল পাঠানোর কোডটিকে `try-catch` ব্লকের ভেতর রেখেছি এবং কোনো এরর আসলে তা ইগনোর (Swallow) করেছি। এর মানে হলো— আমরা ইমেইল পাঠানোর সর্বোচ্চ চেষ্টা করবো, কিন্তু ইমেইল ফেইল করলেও ইউজার সাকসেসফুলি ক্রিয়েট হয়ে যাবে। ইউজার পরে "Resend Email" করে নিতে পারবে। 
*   **দুর্বলতা:** ইমেইল ফেইল করলে সিস্টেম পুরোপুরি ভুলে যায় যে ওই ইউজারকে ইমেইল পাঠানো হয়নি। 

**The Architect Solution (The Outbox Pattern):**
যদি ইমেইল পাঠানোটা ১০০% রিলায়েবল (Guaranteed) হতে হয়, তখন "Outbox Pattern" ব্যবহার করা হয়।
১. ডেটাবেসে ইউজার সেভ করার সময় একই ট্রানজেকশনের ভেতরে `OutboxMessages` নামে আরেকটি টেবিলে সেভ করা হয় যে— "এই ইউজারকে ইমেইল পাঠাতে হবে।"
২. মূল রিকোয়েস্ট থেকে সরাসরি কোনো ইমেইল পাঠানো হয় না। ইউজার সাথে সাথে রেসপন্স পেয়ে যায়।
৩. ব্যাকগ্রাউন্ডে একটি ওয়ার্কার (যেমন: Hangfire বা MassTransit) সারাক্ষণ `Outbox` টেবিল চেক করে এবং ইমেইল সেন্ড করে।
৪. যদি SendGrid ডাউন থাকে, ইমেইল ফেইল করবে, কিন্তু মেসেজটি ডেটাবেসেই থেকে যাবে। ওয়ার্কার ৫ মিনিট পর আবার চেষ্টা করবে (Retries)। এটি নিশ্চিত করে যে ইমেইল একসময় না একসময় যাবেই (At-Least-Once Delivery)।

### 🇬🇧 English

**The "Best-Effort" Email Delivery Pattern:**
In the `Signup` flow, sending an email directly within the API request is risky. If the email provider (e.g., SendGrid) fails, it could roll back the entire database transaction, preventing user creation. 
To avoid this bad UX, we wrap the email logic in a `try-catch` block and swallow the exception. We make our "best effort" to send it, but if it fails, the user is still created. The downside is that the system "forgets" the email failed, and the user must manually request a resend.

**The Outbox Pattern (Guaranteed Delivery):**
For mission-critical notifications, the Outbox Pattern is the architectural standard. 
Instead of sending the email during the HTTP request, an event (e.g., `UserCreatedEvent`) is saved into an `OutboxMessages` table within the **same ACID transaction** as the user creation. A background worker (like Hangfire or MassTransit) continuously polls the outbox and attempts to send the email. If the email provider is down, the worker simply retries later. This guarantees **At-Least-Once Delivery** without sacrificing the reliability of the main API request.

---
