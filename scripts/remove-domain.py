import os
import re

def replace_in_file(file_path, pattern, replacement):
    """Replace regex pattern in a single file."""
    try:
        with open(file_path, "r", encoding="utf-8") as f:
            content = f.read()

        new_content = re.sub(pattern, replacement, content)

        # Only write if something changed
        if new_content != content:
            with open(file_path, "w", encoding="utf-8") as f:
                f.write(new_content)
            print(f"Updated: {file_path}")

    except (UnicodeDecodeError, PermissionError):
        # Skip binary or unreadable files
        print(f"Skipped (not text or no permission): {file_path}")


def replace_in_folder(folder_path, pattern, replacement):
    """Walk through folder and replace regex in all files."""
    for root, _, files in os.walk(folder_path):
        for name in files:
            file_path = os.path.join(root, name)
            replace_in_file(file_path, pattern, replacement)


if __name__ == "__main__":
    replace_in_folder("../resources/assets/quiversandsheaths/shapes", "game:", "")
