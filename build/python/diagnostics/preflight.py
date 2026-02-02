import shutil
from pathlib import Path


def run_preflight(root: Path) -> tuple[bool, list[str]]:
    messages = []
    required = ["dotnet", "git"]
    missing = [tool for tool in required if shutil.which(tool) is None]
    if missing:
        messages.append(f"Missing tools: {', '.join(missing)}")
    config = root / "config" / "appsettings.json"
    if not config.exists():
        messages.append("config/appsettings.json missing (run make setup-config)")
    return len(messages) == 0, messages
