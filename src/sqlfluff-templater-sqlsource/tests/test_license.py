from pathlib import Path

PROJECT = Path(__file__).resolve().parents[1]


# The package ships its own copy of the repository's licence, since hatchling packs only files under the project's
# folder.
def test_license_is_the_repositorys():
    assert (PROJECT / "LICENSE").read_bytes() == (PROJECT.parents[1] / "LICENSE").read_bytes()
