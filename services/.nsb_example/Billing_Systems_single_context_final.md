# **Contract: Billing Systems**

- Template Use Instructions:
  - Use this template as a way to express a Domain Model of a single Context for AI to consume.
  - Before writing ---, insert a blank line if the previous line isn't blank.
  - If a name is missing for Event, output the label without a space after the colon like this - **Event:**
  - No trailing spaces anywhere.
  - Blank line before and after headings (your rule).
  - Programming Hint:

Contracts are the outermost boundary of an implementation. Each Contract typically manages changes in a single Context.

- *Contract Meta*
  - Purpose Statement: <>
  - Stakeholders: <>
  - Version #: 1.0.1

---

## **Context: Billing**

- Programming Hint: A Context contains Modules, Units of Work, and Personas (for informational purposes).

  - *Context Meta*
    - Description: <>
    - Development Type: Greenfield
    - Version #: 1.0.1
    - ContextType: <>

### **Context Elements for Billing**

#### **Personas for Billing**

- *No personas defined*

##### **Modules for Billing**

- **Module: Billing**
  - *Module Meta*
    - (none)
  - **Views for Module Billing**
    - **View: Billing**
      - *View Meta*
        - (none)
      - *Data Elements*
        - *No data elements*
  - **Policies for Module Billing**
    - **Policy: MustBillOnOrderPlaced**
      - *Policy Meta*
        - Handler: OnOrderPlaced
        - Parameters: OrderID={GUID}
  - **Units of Work for Module Billing**
    - **Unit of Work: BillingOrder**
      - *Unit of Work Meta*
        - Type: <e.g Transaction Script or Aggregate>
        - InvariantList: <e.g Comma Separated List>
      - **Commands for UoW BillingOrder**
        - **Command: BillOrder**
          - *Command Meta*
            - Return: {}
          - *Parameters*
            - OrderID: {GUID}
      - **Events for UoW BillingOrder**
        - **Event: OrderBilled**
          - *Event Meta*
            - Published: Yes
          - *Data Elements*
            - OrderID: {GUID}

---

#### **Context Relationships**

##### **Persona Relationships**

- *No persona relationships*

##### **View Relationships**

- *No view relationships*

##### **Policy Relationships**

- **Policy [1-1]**
  - *Policy*: MustBillOnOrderPlaced
  - *View: Billing*
  - *Response Messages*
    - Billing.OrderPlacedHandler[0]
      Received OrderPlaced, OrderId = {OrderID} - Charging credit card...Publishing OrderBilled
  - *Command: BillOrder*
  - *External Subscriptions*
    - Sales.OrderPlaced

---

### **Unit of Work Functions**

- **Unit of Work: BillingOrder**
  - **Command: BillOrder**
  - **Logic Details**
    - Intuitive
  - **Event: OrderBilled**
  - **Compensatory Instructions**
    - **Event:**

---

## **Additional Considerations**

- *(none)*
