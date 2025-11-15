# TODO List for Refactoring Edit Functionality and Visual Improvements

## Parte 1: Convertir Editar de Modales a Páginas Separadas

### Products
- [ ] Create EditProduct.razor page (copy content from EditProductModal.razor, adapt for page)
- [ ] Update ListProducts.razor: Change edit button to navigate to /products/edit/{id}, remove modal rendering and related code

### Customers
- [ ] Create EditCustomer.razor page (copy content from EditCustomerModal.razor, adapt for page)
- [ ] Update ListCustomers.razor: Change edit button to navigate to /customers/edit/{id}, remove modal rendering and related code

### Taxes
- [ ] Create EditTax.razor page (copy content from EditTaxModal.razor, adapt for page)
- [ ] Update ListTaxes.razor: Change edit button to navigate to /taxes/edit/{id}, remove modal rendering and related code

### Users
- [ ] Create EditUser.razor page (extract inline modal form from ListUsers.razor, adapt for page)
- [ ] Update ListUsers.razor: Change edit button to navigate to /users/edit/{id}, remove modal rendering and related code

## Parte 2: Mejoras Visuales

### Tablas en Listados
- [ ] Update all list pages (Products, Customers, Taxes, Users, Invoices, Purchases, Inventory, Roles) to use better table classes (e.g., table-striped, table-hover, card wrapper)

### Ver Detalles en Productos
- [ ] Improve ProductDetail.razor layout: Better cards, spacing, icons, responsive design

### Agregar Ver Detalles a Otros Menús
- [ ] Create CustomerDetail.razor page (similar to ProductDetail)
- [ ] Add "Ver detalles" button to ListCustomers.razor
- [ ] Create UserDetail.razor page
- [ ] Add "Ver detalles" button to ListUsers.razor
- [ ] Consider for other lists if appropriate (e.g., InvoiceDetail already exists, Purchases/Inventory might not need)

## Testing
- [ ] Test navigation to edit pages
- [ ] Test save functionality in edit pages
- [ ] Test visual improvements
- [ ] Ensure no broken links or missing components
