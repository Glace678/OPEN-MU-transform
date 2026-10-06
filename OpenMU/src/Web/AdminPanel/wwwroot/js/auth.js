// Exchanges a single use sign in ticket for the authentication cookie, without navigating away.
export async function signIn(ticket) {
    const response = await fetch('/auth/complete', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ticket: ticket }),
        credentials: 'same-origin',
        cache: 'no-store'
    });
    return response.ok;
}

// Removes the authentication cookie, without navigating away.
export async function signOut() {
    // The logout endpoint requires an antiforgery token; it is rendered as a hidden field by App.razor.
    const tokenField = document.querySelector('input[name="__RequestVerificationToken"]');
    const headers = {};
    if (tokenField) {
        headers['RequestVerificationToken'] = tokenField.value;
    }

    const response = await fetch('/auth/logout', {
        method: 'POST',
        headers: headers,
        credentials: 'same-origin',
        cache: 'no-store'
    });
    return response.ok;
}
