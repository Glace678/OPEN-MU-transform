export async function postJson(url, payload) {
    const response = await fetch(url, {
        method: "POST",
        credentials: "same-origin",
        headers: {
            "Accept": "application/json",
            "Content-Type": "application/json"
        },
        body: JSON.stringify(payload)
    });

    let result;
    try {
        result = await response.json();
    } catch {
        result = null;
    }

    return {
        success: result?.success === true,
        code: result?.code ?? (response.ok ? "error" : `http_${response.status}`),
        message: result?.message ?? "The server did not return a valid response."
    };
}
