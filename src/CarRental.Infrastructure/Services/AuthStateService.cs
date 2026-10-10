using CarRental.Core.Entities;
using CarRental.Core.Enums;
using CarRental.Core.Interfaces;

namespace CarRental.Infrastructure.Services;

public class AuthStateService : IAuthStateService
{
    private User _currentUser;
    private bool _isAuthenticated = false;

    public AuthStateService()
    {
        _currentUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            FullName = "المدير العام (أدمن)",
            Role = UserRole.Admin,
            PhoneNumber = "0501234567"
        };
    }

    public User CurrentUser => _currentUser;
    public bool IsAuthenticated => _isAuthenticated;

    public event Action? OnUserChanged;

    public void Login(User user)
    {
        _currentUser = user;
        _isAuthenticated = true;
        OnUserChanged?.Invoke();
    }

    public void Logout()
    {
        _isAuthenticated = false;
        OnUserChanged?.Invoke();
    }

    public void SetUser(User user)
    {
        _currentUser = user;
        OnUserChanged?.Invoke();
    }
}
