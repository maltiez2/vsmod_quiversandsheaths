import re
import sys
import glob
import os

def load_mappings(rule_file):
    mappings = []

    with open(rule_file, "r", encoding="utf-8") as f:
        for line_num, line in enumerate(f, 1):
            line = line.rstrip("\n")

            if not line or line.lstrip().startswith("#"):
                continue

            if "\t" not in line:
                raise ValueError(
                    f"Line {line_num} has no tab separator: {line}"
                )

            pattern, replacement = line.split("\t", 1)
            mappings.append((re.compile(pattern), replacement))

    return mappings

def process_file(path, mappings):
    with open(path, "r", encoding="utf-8", errors="ignore") as f:
        text = f.read()

    original = text

    for regex, replacement in mappings:
        text = regex.sub(replacement, text)

    if text != original:
        with open(path, "w", encoding="utf-8") as f:
            f.write(text)
        return True

    return False

def collect_files(glob_path):
    matched = glob.glob(glob_path, recursive=True)
    files = []

    for path in matched:
        if os.path.isfile(path):
            files.append(path)

    return files

def main(target_glob, rule_file):
    mappings = load_mappings(rule_file)
    files = collect_files(target_glob)

    modified = 0

    for path in files:
        try:
            if process_file(path, mappings):
                modified += 1
        except Exception as e:
            print(f"Skipped {path}: {e}")

    print(f"Done. Modified {modified} file(s).")

if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Usage: python regex_replace_folder.py <glob_path> <replacements.txt>")
        sys.exit(1)

    main(sys.argv[1], sys.argv[2])
