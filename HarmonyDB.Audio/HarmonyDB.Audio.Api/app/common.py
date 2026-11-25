"""What the two services' code shares."""
from __future__ import annotations


def once_in_a_row(tracks: list[dict]) -> list[dict]:
    """A track that stands in a list several times in a row is given once."""
    out = []
    for t in tracks:
        if not (out and t.get("id") and out[-1].get("id") == t.get("id")):
            out.append(t)
    return out
