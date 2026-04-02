# Kidnap mechanic

## Design summary
- New action: **Kidnap Character**
- Role: offensive **Agent** action
- Minimum agent skill: **4**
- Target: enemy **non-leader**, non-army-commander character in the same hex
- On success, the target is removed from their owner's controlled character list and becomes a captive of the kidnapper
- Each captive causes their original owner to lose **1 gold per turn**
- Each turn, captives have a chance to **escape** based on the kidnapper's current agent level
- If the kidnapper dies, captives are released automatically

## Current implementation choices
- Escape roll each turn: `Random.Range(0, 10) >= kidnapper.GetAgent()` means stronger agents hold captives more reliably
- Captives remain in the kidnapper's current hex
- Leaders and army commanders cannot be kidnapped
- A kidnapped character cannot act while captive because they are removed from their owner roster
- The card is added to the shared `ActionsDeck`, so it appears in all base decks through the common shared action layer

## Notes
- This is implemented as a first-pass systems version
- It is mechanically functional, but UI surfacing for listing prisoners on character panels may still be worth adding later
- Balance values currently chosen:
  - difficulty: 65
  - reward XP: 2
  - required agent skill: 4
