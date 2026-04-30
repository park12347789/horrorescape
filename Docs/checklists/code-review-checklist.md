# Code Review Checklist

## Live Loop Safety

- Does this keep the canonical `RMainEscape_Lobby -> RMainScene_5F~1F` loop
  behaving correctly?
- Could this break lobby start-run, retry, floor-clear, failure, or return flow?
- If player persistence changed, are save/restore paths still correct?

## Architecture

- Is the responsibility of this class, scene object, or prefab change clear?
- Does UI read state instead of owning gameplay rules?
- Are dependencies explicit instead of hidden global lookups?
- If the change is cross-cutting, should it be captured in an ADR?

## Unity Usage

- Are `Find`, `SendMessage`, and repeated `GetComponent` calls avoided in
  runtime paths?
- Are component references cached outside hot paths?
- Does this respect the authored scene contract instead of introducing another
  fragile fallback path?
- If runtime-loaded content expanded, should this stay on the current
  `Resources` pattern or does it need an ADR?

## Performance

- Are there avoidable allocations in frequently called code?
- Is update frequency appropriate for the behavior?
- Would an event, coroutine, or explicit state transition be cleaner than more
  polling?

## Testing And Validation

- What behavior changed?
- What is most likely to regress?
- Is there an EditMode test, PlayMode test, validator, or manual verification
  note?
- Are null, empty, interrupted, and scene-miswired states covered?
