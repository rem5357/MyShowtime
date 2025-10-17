using Microsoft.JSInterop;
using MyShowtime.Shared.Dtos;
using System.Text.Json;

namespace MyShowtime.Client.Services;

public class UserStateService
{
    private const string StorageKey = "currentUser";
    private readonly IJSRuntime _jsRuntime;
    private UserDto? _currentUser;

    public event Action? OnUserChanged;

    public UserStateService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public UserDto? CurrentUser => _currentUser;

    public bool IsLoggedIn => _currentUser is not null;

    public async Task InitializeAsync()
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (!string.IsNullOrWhiteSpace(json))
            {
                _currentUser = JsonSerializer.Deserialize<UserDto>(json);
                // Notify subscribers that user state has been loaded
                OnUserChanged?.Invoke();
            }
        }
        catch
        {
            // If there's any error reading from localStorage, just start fresh
            _currentUser = null;
        }
    }

    public async Task LoginAsync(UserDto user)
    {
        _currentUser = user;
        var json = JsonSerializer.Serialize(user);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        OnUserChanged?.Invoke();
    }

    public async Task LogoutAsync()
    {
        _currentUser = null;
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        OnUserChanged?.Invoke();
    }
}
