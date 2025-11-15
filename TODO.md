# TODO: Cambiar Editar Usuarios a Navegación en Página Separada

## Pasos a Completar

- [x] Modificar ListUsers.razor: Cambiar el botón de editar de modal a enlace de navegación a "/users/edit/{user.Id}"
- [x] Eliminar el código del modal en ListUsers.razor: Remover el bloque @if (showEditModal), variables relacionadas (showEditModal, editingUserModel), y métodos (OpenEditModal, CloseEditModal, HandleUpdateUser)
- [x] Verificar que EditUser.razor ya maneje la edición correctamente (ya existe)
- [x] Probar la navegación después de los cambios
