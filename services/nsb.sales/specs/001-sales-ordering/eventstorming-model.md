# Contract: Sales Systems

- Template Use Instructions:
  - Use this template as a way to express a Domain Model of a single Context for AI to consume.
  - Before writing ---, insert a blank line if the previous line isn't blank.
  - If a name is missing for Event, output the label without a space after the colon like this - **Event:**
  - No trailing spaces anywhere.
  - Blank line before and after headings (your rule).
  - Programming Hint:

Contracts are the outermost boundary of an implementation. Each Contract typically manages changes in a single Context.

- *Contract Meta*
  - Purpose Statement: Implement the Sales Capability
  - Stakeholders: John
  - Version #: 1.0.1

---

## **Context: Sales**

- Programming Hint: A Context contains Modules, Units of Work, and Personas (for informational purposes).

  - *Context Meta*
    - Description: <>
    - Development Type: Greenfield
    - Version #: 1.0.1

### **Context Elements for Sales**

#### **Personas for Sales**

- **Persona: Client**
  - *Persona Meta*
    - Notes: Connolly, John

##### **Modules for Sales**

- **Module: Ordering**
  - *Module Meta*
    - (none)
  - **Views for Module Ordering**
    - **View: Sales**
      - *View Meta*
        - Display: Press 'P' to place an order, or 'Q' to quit.
      - *Data Elements*
        - OrderID: {GUID}
  - **Policies for Module Ordering**
    - **Policy: MustPlaceOrder**
      - *Policy Meta*
        - Handler: OnPlaceOrder
        - Parameters: OrderID={GUID}
  - **Units of Work for Module Ordering**
    - **Unit of Work: PlacingOrder**
      - *Unit of Work Meta*
        - Type: Transaction Script
        - InvariantList: <e.g Comma Separated List>
      - **Commands for UoW PlacingOrder**
        - **Command: PlaceOrder**
          - *Command Meta*
            - Return: {}
          - *Parameters*
            - OrderID: {GUID}
      - **Events for UoW PlacingOrder**
        - **Event: OrderPlaced**
          - *Event Meta*
            - Published: Yes
          - *Data Elements*
            - OrderID: {GUID}

---

#### **Context Relationships**

##### **Persona Relationships**

- **Persona [1-many]**
  - *Persona*: Client
  - *View: Sales*

##### **View Relationships**

- **View [1-1]**
  - *View*: Sales
  - *Policy: MustPlaceOrder*

##### **Policy Relationships**

- **Policy [1-1]**
  - *Policy*: MustPlaceOrder
  - *View: Sales*
  - *Response Messages*
    - Sending PlaceOrder command, OrderId = {OrderID}
  - *Command: PlaceOrder*
  - *External Subscriptions*
    - *No external subscriptions*

---

### **Unit of Work Functions**

- **Unit of Work: PlacingOrder**
  - **Command: PlaceOrder**
  - **Logic Details**
    - Intuitive
  - **Event: OrderPlaced**
  - **Compensatory Instructions**
    - **Event:**

---

## **Additional Considerations**

- *(none)*
