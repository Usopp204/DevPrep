"""GET /catalog — roles and technologies available in the indexed dataset.

The dashboard uses this so it only offers roles/technologies that actually have questions.
- Roles = real job roles (positions minus the cross-cutting skill tracks).
- Technologies for a role = that role's own categories plus the accompanying skill tracks
  (Database, Language), since e.g. a Backend role commonly pairs with a database and a language.
"""
from fastapi import APIRouter

from app.services import chroma_store

router = APIRouter()

# Tracks that are NOT standalone job roles. Database/Language are accompanying skills;
# Problem Solving / System Design are woven in automatically by the planner.
CROSS_CUTTING = {"Database", "Language", "System Design", "Problem Solving"}
ACCOMPANYING = ("Database", "Language")


@router.get("/catalog")
def get_catalog() -> dict:
    cat = chroma_store.catalog()  # {position: [categories]}
    roles = sorted(p for p in cat if p not in CROSS_CUTTING)

    accompanying: list[str] = []
    for track in ACCOMPANYING:
        accompanying += cat.get(track, [])

    technologies = {
        role: sorted(set(cat.get(role, []) + accompanying))
        for role in roles
    }
    return {"roles": roles, "technologies": technologies}