using CarRental.Core.Entities;
using CarRental.Core.Enums;

namespace CarRental.Core.Interfaces;

public interface IAuthStateService
{
    User CurrentUser { get; }
    UserRole CurrentRole => CurrentUser.Role;
    bool IsAdmin => CurrentRole == UserRole.Admin;
    bool IsStaff => CurrentRole == UserRole.Staff;
    bool IsAccountant => CurrentRole == UserRole.Accountant;
    bool IsAuthenticated { get; }
    
    event Action? OnUserChanged;
    void SetUser(User user);
    void Login(User user);
    void Logout();
}
