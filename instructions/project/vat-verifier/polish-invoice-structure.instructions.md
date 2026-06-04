---
name: polish-invoice-structure
description: Polish invoice structure knowledge — KSeF FA(3) schema, invoice parties, required fields, line items, invoice types, identification numbers, and KSeF mandatory dates
metadata:
  type: knowledge
  project: vat-verifier
  sources:
    - KSeF FA(3) logical structure specification (Ministry of Finance)
    - Article 106e of the Polish VAT Act
    - https://ksef.podatki.gov.pl
    - https://ksefgpt.pl/en/wiedza/guide/xml-and-the-fa-3-schema-in-ksef-a-guide-for-businesses
    - https://www.vatupdate.com/2025/10/24/ksef-e-invoice-format-mandatory-data/
    - https://polishtax.com/information/polish-tax-law/issuance-of-the-invoices/
---

# Polish Invoice Structure Knowledge

## KSeF overview

From 1 February 2026, all standard B2B domestic VAT invoices in Poland must be issued through **KSeF** (Krajowy System e-Faktur) in structured XML format. KSeF assigns each invoice a unique identifier (`NumerKSeF`) after acceptance.

The current active schema is **FA(3)**, published by the Ministry of Finance.

---

## Top-level FA(3) elements

| Element | Mandatory | Purpose |
|:---|:---|:---|
| `Naglowek` | Yes | Schema identification and document metadata |
| `Podmiot1` | Yes | Seller (invoice issuer) |
| `Podmiot2` | Yes (B2B) / conditional (B2C) | Buyer |
| `Podmiot3` | No | Additional participants (up to 100) |
| `PodmiotUpowazniony` | No | Authorized issuer (self-billing or agent) |
| `Fa` | Yes | Invoice header — dates, number, type, currency |
| `FaWiersz` (inside `Fa`) | Yes | Line items (one element per product/service) |
| `Podsumowanie` (inside `Fa`) | Yes | VAT totals aggregated by rate |
| `Platnosc` (inside `Fa`) | No | Payment terms and method |
| `Stopka` | No | Footer — REGON, KRS, other optional registry numbers |

---

## Invoice parties

### Podmiot1 — Seller

Represents the taxpayer issuing the invoice. Required fields:

| Field | Description | Format |
|:---|:---|:---|
| `NIP` | Polish tax identification number | 10 digits, no spaces or dashes |
| `Nazwa` | Full registered company name or personal name | Up to 256 characters |
| `AdresDzialalnosci` — `KodKraju` | Country code | ISO 3166-1 alpha-2 (e.g., `PL`) |
| `AdresDzialalnosci` — `Ulica` | Street and building number | |
| `AdresDzialalnosci` — `Miejscowosc` | City | |
| `AdresDzialalnosci` — `KodPocztowy` | Postal code | Format: `XX-XXX` |

The seller's NIP is a hard requirement — an invoice cannot be issued in KSeF without it.

### Podmiot2 — Buyer

Represents the purchaser. Rules differ by buyer type:

| Buyer type | Identification | Notes |
|:---|:---|:---|
| Polish business (VAT-registered) | `NIP` (10 digits) | Required for B2B; KSeF delivers invoice to buyer's account |
| Polish business (VAT-exempt) | `NIP` | Still required |
| EU business | `KodUE` + `NrVatUE` | EU country code + EU VAT number |
| Foreign business (non-EU) | `NrID` | Foreign tax ID |
| Private individual | `BrakID = 1` | No tax ID — buyer section may be omitted or simplified |

Buyer's address structure mirrors Podmiot1 (country code, street, city, postal code).

### Podmiot3 — Additional parties

Used for scenarios involving multiple recipients or intermediaries. Each entry has:
- A role code identifying their function in the transaction
- Identification fields (NIP or other ID) and address
- Up to 100 Podmiot3 entries per invoice

Common use cases: split payment with multiple payees, factoring, triangular transactions.

### PodmiotUpowazniony — Authorized issuer

Used when the invoice is issued by someone other than the seller:
- `RolaPU` — role code (e.g., tax representative, bailiff, court-appointed liquidator)
- Identification and address fields

---

## Mandatory invoice header fields (Fa element)

| Field | Description | Format / Values |
|:---|:---|:---|
| `RodzajFaktury` | Invoice type | See invoice types table below |
| `P_1` | Invoice issue date | `YYYY-MM-DD` |
| `P_2` | Sequential invoice number | Issuer-assigned; must be unique within the issuer |
| `P_6` | Sale / supply date (if different from issue date) | `YYYY-MM-DD` |
| `KodWaluty` | Currency code | ISO 4217 (e.g., `PLN`, `EUR`, `USD`) |
| `P_15` | Total gross amount | Decimal, 2 places |
| `DataWytworzeniaFa` | XML generation timestamp | `YYYY-MM-DDTHH:MM:SSZ` (UTC) |

---

## Invoice types (RodzajFaktury)

| Code | Polish name | Use |
|:---|:---|:---|
| `VAT` | Faktura VAT | Standard sales invoice |
| `KOR` | Faktura korygująca | Corrective invoice (credit/debit note) |
| `ZAL` | Faktura zaliczkowa | Advance payment invoice |
| `ROZ` | Faktura rozliczeniowa | Final invoice settling prior advance(s) |
| `UPR` | Faktura uproszczona | Simplified invoice (up to 450 PLN or 100 EUR gross) |

Corrective invoices (`KOR`) require additional fields:
- `NrFaKorygowanej` — original invoice number
- `DataWystawieniaFaKorygowanej` — original invoice date
- `PrzyczynaKorekty` — reason for correction (optional text, max 256 characters)
- `NrKSeFFaKorygowanej` — KSeF ID of the original (if originally issued in KSeF)

---

## Line item fields (FaWiersz)

Each product or service is one `FaWiersz` element.

| Field | Description | Mandatory | Notes |
|:---|:---|:---|:---|
| `NrWierszaFa` | Line sequence number | Yes | Integer, starting from 1 |
| `P_7` | Product or service description | Yes | Up to 512 characters |
| `P_8A` | Unit of measure code | Yes | E.g., `szt.` (piece), `kg`, `m`, `godz.` (hour), `usł.` (service unit) |
| `P_8B` | Quantity | Yes | Decimal |
| `P_9A` | Net unit price (excluding VAT) | Yes | Decimal, 2 places |
| `P_12` | VAT rate | Yes | `23`, `8`, `5`, `0`, `ZW`, `NP` |
| `P_11` | Net line amount (quantity × net unit price) | Computed | System validates: P_8B × P_9A = P_11 |
| `P_10` | Discount / surcharge amount | No | Negative for discount |
| `P_6A` | Delivery or performance date for this line | No | Overrides header P_6 at line level |
| `P_11A` | VAT exemption basis per line | No | Required when P_12 = ZW |
| `PKWiU` | Polish Classification of Products and Services code | No | Required for certain goods by tax authority guidance |
| `CN` | Combined Nomenclature (EU customs) code | No | For goods |
| `GTU` | Good/Service Type code (1–13) | No | Required for SAF-T/JPK reporting for certain goods |

### GTU codes (selected)

| GTU code | Category |
|:---|:---|
| GTU_01 | Alcoholic beverages |
| GTU_02 | Tobacco products and electronic cigarettes |
| GTU_03 | Fuel and heating oil |
| GTU_04 | High-value metal goods |
| GTU_05 | Waste, scraps |
| GTU_06 | Electronics (phones, computers, gaming consoles) |
| GTU_07 | Vehicles and vehicle parts |
| GTU_08 | Precious metals, jewellery |
| GTU_09 | Pharmaceuticals under the reimbursement list |
| GTU_10 | Construction services |
| GTU_11 | Greenhouse gas emission allowances |
| GTU_12 | Intangibles (software, licences, rights) |
| GTU_13 | Transport and storage services |

---

## VAT summary fields (Podsumowanie)

| Field | Description | VAT rate |
|:---|:---|:---|
| `P_13_1` / `P_14_1` | Net amount / VAT amount at 23% | 23% |
| `P_13_2` / `P_14_2` | Net amount / VAT amount at 8% | 8% |
| `P_13_3` / `P_14_3` | Net amount / VAT amount at 5% | 5% |
| `P_13_6` / `P_14_6` | Net amount / VAT amount at 0% | 0% |
| `P_13_7` | Net amount of exempt supplies | ZW |

Validation rule: Σ(all P_13_x net amounts) + Σ(all P_14_x VAT amounts) = P_15 (total gross).

---

## Invoice annotation flags (Adnotacje section)

| Field | Value | Meaning |
|:---|:---|:---|
| `P_16` | `true` | Cash accounting method (metoda kasowa) |
| `P_17` | `true` | Self-billing (samofakturowanie) |
| `P_18` | `true` | Reverse charge applies (odwrotne obciążenie) |
| `P_18A` | `true` | Split payment required (mechanizm podzielonej płatności) |
| `P_19` | Legal basis reference | VAT exemption basis (for ZW supplies) |

---

## Payment fields (Platnosc section — optional)

| Field | Description | Values |
|:---|:---|:---|
| `TerminPlatnosci` | Payment due date | `YYYY-MM-DD` |
| `FormaPlatnosci` | Payment method | 1=cash, 2=card, 3=voucher, 4=cheque, 5=credit, 6=transfer, 7=mobile |
| `RachunekBankowy` | IBAN for payment | Polish IBANs: `PL` + 26 digits |
| `Zaplacono` | Payment already made flag | `true` / `false` |
| `DataZaplaty` | Date of payment | `YYYY-MM-DD` |

---

## Polish identification number formats

| ID type | Description | Format | Used by |
|:---|:---|:---|:---|
| NIP | Tax identification number (Numer Identyfikacji Podatkowej) | 10 digits: `XXXXXXXXXX` | All registered businesses; mandatory for VAT payers |
| PESEL | Personal ID number | 11 digits | Private individuals; not printed on B2B invoices |
| REGON | National Business Registry number | 9 or 14 digits | Optional on invoice footer; identifies legal entity |
| KRS | National Court Register number | Up to 10 digits | For companies registered with the court |
| EU VAT | Intra-EU VAT number | Country prefix + digits (e.g., `DE123456789`) | EU foreign buyers |

**NIP validation:** 10 digits with a checksum based on weights [6,5,7,2,3,4,5,6,7]; last digit is the check digit. An invalid NIP causes KSeF to reject the invoice.

---

## Key KSeF validation rules

- All monetary amounts: 2 decimal places; PLN standard
- Line-level math: system validates P_8B × P_9A = P_11 (with rounding tolerance)
- Invoice total: Σ(net amounts) + Σ(VAT amounts) = P_15
- Date format strictly `YYYY-MM-DD`; timestamps in `YYYY-MM-DDTHH:MM:SSZ` (UTC)
- Invoice numbers must be sequential and unique per issuer; gaps are allowed but may trigger queries
- KSeF assigns `NumerKSeF` after successful submission; this ID is used for corrections and buyer references
- Corrective invoices show the **difference** relative to the original, not the new absolute values
- QR code with KSeF ID is required if the invoice is shared outside the KSeF system (printed or emailed)
