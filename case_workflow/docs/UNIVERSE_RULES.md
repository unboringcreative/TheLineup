# The Lineup Universe Rules

This file is the living rulebook for case generation.

## Core Rules

1. Exactly one red herring per case.
   - Each case must include one clue that appears incriminating but is ultimately not part of the true chain of proof.
   - The final explanation should identify why it was misleading.

2. No text on evidence images.
   - Evidence images must communicate clues visually without readable words or labels.
   - Avoid prompts that rely on legible text as the clue itself.

3. Lineup portrait scale follows suspect height.
   - Use 5'9" as the visual baseline scale (1.00x).
   - Scale portraits by 1.25% per inch difference from baseline.
   - Clamp final lineup scale between 0.85x and 1.18x to avoid extreme distortion.

4. Cases are solved through evidence, not vibes.
   - The guilty suspect must be the only suspect consistent with the full clue chain.
   - Final logic should rely on material transfer, timing, access, contradiction, or custody evidence.

5. Institutions must be concrete and believable.
   - Every case should name a specific workplace, archive, office, terminal, depot, or similar institution.
   - The institution should naturally explain who had access, what was valuable, and how the breach could occur.

6. Geography must stay coherent.
   - City, country, architecture, workplace naming, and suspect backgrounds should point to the same place.
   - Do not mix signals from unrelated regions unless the case explicitly supports that mix.

7. Suspects need distinct social texture.
   - Each suspect should have a different role, pressure point, and interview cadence.
   - Bios should feel grounded rather than like shuffled trait cards.

8. Evidence roles should be differentiated.
   - Every case should include exactly one valid evidence item, one red-herring evidence item, and one neutral evidence item.
   - The valid evidence should point toward the guilty suspect.
   - The red herring should point toward the wrong suspect.
   - The neutral evidence should add context, timing, or atmosphere without directly accusing anyone.
   - Avoid three clues that all say the same thing in different wording.

9. Explanations must close the loop.
   - Name the guilty suspect and slot explicitly.
   - Explain why the valid evidence matters.
   - Identify the red herring and why it fails.
   - Clarify how the neutral evidence fits the case without becoming an accusation.

10. Tone stays pulp-noir, not supernatural.
    - Atmosphere can be stylized, moody, and heightened.
    - Resolutions should still come from grounded human actions and credible evidence.

## Timeline And Logic

- The incident should happen inside a clear, narrow time window.
- Every suspect should have plausible opportunity, but only one should survive full scrutiny.
- Dialogue can misdirect, but it should not make the final solution arbitrary.
- Motives should be concrete: concealment, leverage, debt, career protection, smuggling, sabotage, or adjacent pressures.

## Names And Places

- Prefer grounded full names over novelty names.
- Keep workplace names specific enough to anchor the scene quickly.
- Use place details that enrich visual identity without bloating prompts.
- If the setting is international, the nationality mix should still feel institutionally plausible.

## Location Image Rule

- Each case should include a location image for the suspect lineup background.
- The location image should show floor plane, wall depth, or architectural structure so suspect portraits feel grounded rather than pasted onto a flat void.
- Location images can never contain people or characters.
- Location prompts should explicitly say `no people` and `no characters`.
- It does not need to be perspective-perfect, but it should feel like a believable place where the lineup could stand.

## Notes

- Add new rules here as numbered items.
- Treat this document as canonical when writing future case JSON files and prompts.
