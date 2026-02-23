# **Contract: Shipping Systems**

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

## **Context: Shipping**

- Programming Hint: A Context contains Modules, Units of Work, and Personas (for informational purposes).

  - *Context Meta*
    - Description: <>
    - Development Type: Greenfield
    - Version #: 1.0.1
    - ContextType: <>

### **Context Elements for Shipping**

#### **Personas for Shipping**

- *No personas defined*

##### **Modules for Shipping**

- **Module: Shipping**
  - *Module Meta*
    - (none)
  - **Views for Module Shipping**
    - **View: Shipping**
      - *View Meta*
        - (none)
      - *Data Elements*
        - *No data elements*
  - **Policies for Module Shipping**
    - **Policy: MustReserveOnOrderPlaced**
      - *Policy Meta*
        - Handler: OnOrderPlaced
        - Parameters: OrderID={GUID}
    - **Policy: MustShipOnOrderBilled**
      - *Policy Meta*
        - Handler: OnOrderBilled
        - Parameters: OrderID={GUID}
  - **Units of Work for Module Shipping**
    - **Unit of Work: ReserveInventory**
      - *Unit of Work Meta*
        - Type: <e.g Transaction Script or Aggregate>
        - InvariantList: <e.g Comma Separated List>
      - **Commands for UoW ReserveInventory**
        - **Command: ReserveInventory**
          - *Command Meta*
            - Return: {}
          - *Parameters*
            - OrderID: {GUID}
      - **Events for UoW ReserveInventory**
        - **Event: InventoryReserved**
          - *Event Meta*
            - Published: Yes
          - *Data Elements*
            - *No data elements*
    - **Unit of Work: ShipOrder**
      - *Unit of Work Meta*
        - Type: <e.g Transaction Script or Aggregate>
        - InvariantList: <e.g Comma Separated List>
      - **Commands for UoW ShipOrder**
        - **Command: ShipOrder**
          - *Command Meta*
            - Return: {}
          - *Parameters*
            - OrderID: {GUID}
      - **Events for UoW ShipOrder**
        - **Event: OrderShipped**
          - *Event Meta*
            - Published: Yes
          - *Data Elements*
            - *No data elements*

---

#### **Context Relationships**

##### **Persona Relationships**

- *No persona relationships*

##### **View Relationships**

- *No view relationships*

##### **Policy Relationships**

- **Policy [1-1]**
  - *Policy*: MustReserveOnOrderPlaced
  - *View: Shipping*
  - *Response Messages*
    - Shipping.OrderPlacedHandler[0]
      Received OrderPlaced, OrderId = {OrderID} - Order Packaging Start...
  - *Command: ReserveInventory*
  - *External Subscriptions*
    - Sales.OrderPlaced
- **Policy [1-1]**
  - *Policy*: MustShipOnOrderBilled
  - *View: Shipping*
  - *Response Messages*
    - Shipping.OrderBilledHandler[0]
      Received OrderBilled, OrderId = {OrderID} - Verify Package and Ship...
  - *Command: ShipOrder*
  - *External Subscriptions*
    - Billing.OrderBilled

---

### **Unit of Work Functions**

- **Unit of Work: ReserveInventory**
  - **Command: ReserveInventory**
  - **Logic Details**
    - Intuitive
  - **Event: InventoryReserved**
  - **Compensatory Instructions**
    - **Event:**
- **Unit of Work: ShipOrder**
  - **Command: ShipOrder**
  - **Logic Details**
    - Intuitive
  - **Event: OrderShipped**
  - **Compensatory Instructions**
    - **Event:**

---

## **Additional Considerations**

- *(none)*
