---
name: polish-vat-domain
description: Polish VAT domain knowledge — rate structure, goods/services classification by rate, exemption rules, special VAT mechanisms, and edge case taxonomy for test data generation
metadata:
  type: knowledge
  project: vat-verifier
  sources:
    - Polish VAT Act (Ustawa o podatku od towarów i usług)
    - Annex 3 and Annex 10 to the Polish VAT Act
    - https://podatki.gov.pl
    - https://numeral.com/blog/poland-vat-rates-and-compliance
    - https://amavat.pl/stawki-vat-w-polsce-co-musze-wiedziec/
---

# Polish VAT Domain Knowledge

## VAT rate structure

| Rate | Polish name | Code in KSeF (P_12) | Scope |
|:---|:---|:---|:---|
| 23% | stawka podstawowa | 23 | Default — all goods/services not explicitly reduced or exempt |
| 8% | stawka obniżona | 8 | Listed goods/services in Annex 3 to the VAT Act |
| 5% | stawka super obniżona | 5 | Listed goods/services in Annex 10 to the VAT Act |
| 0% | stawka zerowa | 0 | Exports; intra-EU supply of goods; international transport |
| zw. | zwolniony z VAT | ZW | Specific exempt supplies; seller cannot deduct input VAT |
| np. | nie podlega VAT | NP | Outside Polish VAT scope (certain B2B services, intra-EU service rules) |

From 1 January 2026: VAT registration exemption threshold increased to 240,000 PLN annual turnover.

---

## Goods and services by VAT rate

### 23% — standard rate

All goods and services not listed in Annexes 3 or 10 and not exempt. Examples:
- IT services, software development, SaaS subscriptions
- Consulting, legal, accounting services
- Alcoholic beverages (all types, including wine, beer, spirits)
- Tobacco products
- Electronics, computers, phones
- Clothing, footwear (general)
- Motor vehicles and parts
- Fuel and lubricants
- Advertising services
- Entertainment, concerts, cinema tickets
- Cosmetics and non-medical personal care

### 8% — first reduced rate (Annex 3 goods/services)

Food products:
- Sugar, spices, salt, vinegar
- Processed and preserved food not covered by 5%
- Coffee, tea (pure or blended)
- Water (bottled mineral water, spring water)
- Soft drinks and non-alcoholic beverages
- Ice cream

Healthcare and medical:
- Medicinal products and pharmaceuticals (listed in the Pharmaceutical Register or with marketing authorization)
- Medical devices (as classified under the Medical Devices Act)
- Disinfectants with bactericidal, fungicidal, or virucidal properties

Agriculture:
- Seeds and planting materials
- Fertilizers, plant protection products
- Agricultural machinery and parts
- Live animals intended for food production

Construction and housing:
- Supply of residential buildings and units
- Construction services for residential buildings (renovation, repair, modernization)
- Social housing construction (poniżej 150 m² for apartments, 300 m² for houses)

Services:
- Restaurant and catering services (food and non-alcoholic beverages only; alcohol in restaurants is still 23%)
- Passenger transport services (road, rail, air within Poland)
- Tourist services (packages, accommodation in classified establishments)

### 5% — second reduced rate (Annex 10 goods/services)

Basic foodstuffs:
- Meat and meat products (unprocessed, minimally processed)
- Fish and seafood
- Dairy products (milk, cheese, butter, yogurt, cream)
- Eggs
- Cereals, flour, bread, bakery products
- Vegetables (fresh, chilled, frozen)
- Fruit (fresh, chilled, frozen)
- Baby food and special dietary products

Publications:
- Printed books (ISBN-identified)
- Printed journals, newspapers, magazines classified as periodicals
- Digital publications (e-books, online newspapers) from 2022 onwards
- Educational and specialized books

Other:
- Baby diapers and baby dummies
- Feminine hygiene products (from 2023)

### 0% — zero rate

- Exports of goods outside the EU (requires export documentation)
- Intra-Community supply of goods to VAT-registered EU buyers
- International passenger and cargo transport

### zw. (zwolniony) — exempt

Exempt supplies (seller cannot recover input VAT):
- Medical services provided by licensed healthcare entities (doctors, hospitals, clinics)
- Dental services (treatment-related)
- Educational services provided by licensed schools and universities
- Social care and welfare services
- Insurance and financial services (banking, lending, investment funds)
- Postal services (universal service)
- Long-term housing rental (najem mieszkań)
- Cultural services by public or qualifying cultural institutions
- Sports services (participation in sporting activities)
- Lotteries and gambling under state license

**Key rule:** A seller using zw. does not add VAT on the invoice and cannot deduct VAT on related purchases. The invoice must show the legal basis for exemption (art. 43 ust. 1 pkt X of the VAT Act or relevant EU directive article).

---

## Special VAT mechanisms

### Reverse charge (odwrotne obciążenie)

In certain B2B transactions, the VAT obligation shifts from the seller to the buyer:
- Construction services provided by subcontractors to VAT-registered contractors (domestic reverse charge)
- Intra-EU services under art. 28b of the VAT Act (place of supply = buyer's country)
- Cross-border B2B services generally (buyer self-assesses VAT)

On the invoice: net amount only, no VAT amount. The annotation field P_18 is set to `true` in KSeF.

### Split payment (podzielona płatność)

For invoices ≥ 15,000 PLN gross involving goods/services from a statutory "sensitive" list (electronics, fuel, construction materials, steel, precious metals), the buyer must pay:
- Net amount to seller's regular bank account
- VAT amount to seller's dedicated VAT account (rachunek VAT)

On the invoice: the annotation field P_18A in KSeF is set to `true`. The phrase **"mechanizm podzielonej płatności"** must appear on the invoice.

### Cash accounting method (metoda kasowa)

VAT becomes due when payment is received, not when the invoice is issued. Available to small taxpayers (turnover below EUR 2 million per year). On the invoice: P_16 = `true` in KSeF.

### Self-billing (samofakturowanie)

Buyer issues the invoice on behalf of the seller under a prior agreement. On the invoice: P_17 = `true` in KSeF.

### Intra-EU acquisition (WNT / wewnątrzwspólnotowe nabycie towarów)

Buyer self-assesses Polish VAT on goods purchased from EU suppliers. No Polish VAT on the supplier's invoice; buyer records both output and input VAT simultaneously.

---

## Category taxonomy for edge case generation

### High-ambiguity categories (items that easily cross-match)

| Description | Ambiguity type |
|:---|:---|
| "Chopin" | vodka (23%) vs book (5%) vs concert ticket (23%) vs CD (23%) |
| "Woda" | mineral water (8%), bottled bulk (8%), water supply service (8%), technical water (23%) |
| "Olej" | cooking oil (8%), engine oil (23%), heating oil (23%) |
| "Mleko" | dairy product 5% vs fortified milk drink 8% vs lactose-free supplement 8% |
| "Szkolenie" | standard business training (23%) vs medical/safety training — may be exempt (zw.) |
| "Usługa zdrowotna" | licensed healthcare entity → zw.; wellness center → 23% |
| "Aplikacja mobilna" | software development (23%) vs digital publication/e-book (5%) |
| "Wino" | wine is alcohol → 23%, NOT a food item at 8% |
| "Suplement diety" | dietary supplement → may be 5% (food) or 8% (health product) depending on classification |
| "Gazeta" | printed newspaper → 5%; online newspaper → 5%; advertising insert bundled with newspaper → 23% |

### Industry-based supplier vs item mismatch edge cases

| Supplier industry | Unexpected item | Expected behaviour |
|:---|:---|:---|
| Alcohol wholesale | "Woda mineralna" | Water is 8%, but supplier context suggests 23% alcohol |
| Bookstore | "Wódka Chopin 0,7l" | Clear mismatch — cultural brand name, wrong category |
| IT services | "Szkolenie BHP" | BHP safety training may be exempt, not 23% IT |
| Restaurant | "Alkohol serwowany przy posiłku" | Still 23% — alcohol rate applies even in restaurants |
| Medical / pharmacy | "Sprzęt AGD" | General home appliance from medical supplier — still 23% |
| Construction | "Usługi budowlane biurowca" | Commercial construction is 23%, not 8% (residential rule) |

### Typical test case scenario types

| Type | Description | Expected severity |
|:---|:---|:---|
| Clear match, VAT correct | Obvious item, supplier matches, correct VAT rate | `Ok` |
| Clear match, VAT wrong | Obvious item with wrong rate (e.g., vodka at 8%) | `Critical` |
| Ambiguous, same VAT | Item could be two categories, both at same rate | `Warning` |
| Ambiguous, different VAT | Item could be two categories with different rates | `Alert` |
| No category match | Item doesn't fit any defined category | `Alert` |
| Exempt item charged VAT | e.g., medical service with 23% VAT | `Critical` |
| Reverse charge not applied | B2B construction subcontract with VAT charged | `Critical` |

### Common classification errors (most valuable edge cases)

- Books charged at 23% instead of 5%
- Pharmaceuticals charged at 23% instead of 8%
- Restaurant food charged at 23% instead of 8%
- Software subscription charged at 8% instead of 23%
- Medical device labeled as "equipment" at 23% instead of 8%
- Wine/beer charged at 8% (food) instead of 23% (alcohol)
- Digital newspaper charged at 23% instead of 5%
- Long-term apartment rental charged at 8% or 23% instead of exempt
- Construction services for commercial property at 8% instead of 23%
- Mixed bundle (food + alcohol) with a single VAT rate on the invoice

---

## Realistic Polish invoice conventions

### Invoice descriptions (opisy na fakturze)

Realistic patterns:
- Product name + quantity/volume: "Wódka Chopin 0,7l", "Papier A4 ryza 500 arkuszy"
- Service name + period: "Usługa programistyczna marzec 2026", "Abonament SaaS styczeń 2026"
- Service name + project: "Konsultacje IT – projekt Alpha", "Wsparcie techniczne migracja danych"
- Product name + spec: "Olej rzepakowy extra virgin 5L", "Mleko UHT 3,2% 1L"
- Regulatory annotation (if required): "Mechanizm podzielonej płatności" on invoices ≥ 15,000 PLN

### Polish company name conventions

| Legal form | Examples | Typical sector |
|:---|:---|:---|
| Sp. z o.o. | "Alfa Software Sp. z o.o.", "Hurtownia Spożywcza Beta Sp. z o.o." | IT, trade |
| S.A. | "PKN Orlen S.A.", "Poczta Polska S.A." | Large corporations |
| s.c. | "Sklep Spożywczy Jan i Maria Kowalski s.c." | Small shops |
| No suffix (JDG) | "Jan Kowalski – Usługi IT" | Sole proprietors |
| Trade name | "Apteka Pod Orłem", "Hurtownia Alkoholi ABC" | Retail/trade |

### Supplier industry examples

- IT services → "IT services", "firma programistyczna", "software house", "konsulting IT"
- Alcohol wholesale → "hurtownia alkoholi", "alcohol distributor", "dystrybutor napojów"
- Bookstore → "księgarnia", "wydawnictwo", "bookstore", "empik"
- Food wholesale → "hurtownia spożywcza", "dystrybutor żywności", "food distributor"
- Medical / pharmacy → "apteka", "sklep medyczny", "hurtownia farmaceutyczna"
- Construction → "firma budowlana", "usługi remontowe", "contractor"
- Restaurant → "restauracja", "catering", "gastronomy"

---

## Edge case taxonomy

When generating test cases, always include at least one from each category if currently missing:

1. **Linguistic ambiguity** — Polish brand/word with multiple product meanings (Chopin, Woda, Olej)
2. **VAT mismatch on confident match** — item clearly one category, but invoice VAT rate is wrong
3. **Supplier–item mismatch** — supplier industry does not match the product sold
4. **Borderline category** — item genuinely could be classified in two categories (Suplement diety)
5. **Bilingual description** — same item described in Polish vs English (embedding similarity check)
6. **Missing category** — item has no reasonable category match in the seed data
7. **Rate-identical ambiguity** — ambiguous item where all possible categories share the same VAT rate → severity `Warning`, not `Alert`
8. **Agricultural / seasonal** — products that may fall under reduced rates (vegetables, fruit, grain)
9. **Exempt supply misclassified** — medical or educational service incorrectly charged at a positive rate
10. **Commercial vs residential construction** — same service description, different rates depending on building type
