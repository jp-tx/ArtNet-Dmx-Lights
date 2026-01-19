window.app = {
  async api(path, options = {}) {
    const response = await fetch(path, {
      headers: {
        "Content-Type": "application/json",
        ...(options.headers || {})
      },
      ...options
    });

    if (!response.ok) {
      let errorText = "Request failed.";
      try {
        const payload = await response.json();
        if (payload && payload.errors) {
          errorText = payload.errors.join(" ");
        }
      } catch {
        errorText = `${response.status} ${response.statusText}`;
      }
      throw new Error(errorText);
    }

    if (response.status === 204) {
      return null;
    }

    return response.json();
  },
  formatTime(iso) {
    if (!iso) return "";
    const date = new Date(iso);
    return date.toLocaleString();
  }
};
