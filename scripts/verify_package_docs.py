"""Verify that each packaged library ships usable IntelliSense XML beside its DLL."""

import argparse
from pathlib import Path, PurePosixPath
import xml.etree.ElementTree as ET
from zipfile import ZipFile


def verify_packages(directory: Path) -> None:
    packages = sorted(directory.glob("*.nupkg"))
    if not packages:
        raise ValueError(f"No NuGet packages found in {directory}")

    for package in packages:
        with ZipFile(package) as archive:
            libraries = [
                name for name in archive.namelist()
                if name.startswith("lib/") and name.endswith(".dll")
            ]
            if not libraries:
                raise ValueError(f"{package.name}: no library assemblies found")
            for library in libraries:
                xml_path = str(PurePosixPath(library).with_suffix(".xml"))
                if xml_path not in archive.namelist():
                    raise ValueError(f"{package.name}: missing {xml_path}")
                document = ET.fromstring(archive.read(xml_path))
                assembly = PurePosixPath(library).stem
                if document.findtext("assembly/name") != assembly:
                    raise ValueError(f"{package.name}: documentation assembly mismatch")
                members = document.findall("members/member")
                if not members:
                    raise ValueError(f"{package.name}: documentation contains no members")
                for member in members:
                    summary = member.find("summary")
                    if summary is None or not "".join(summary.itertext()).strip():
                        raise ValueError(
                            f"{package.name}: empty summary for {member.get('name')}"
                        )
                print(f"{package.name}: {xml_path} ({len(members)} documented members)")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path, help="Directory containing packed libraries")
    args = parser.parse_args()
    verify_packages(args.directory)
