# Weighted item and movement selection

Item upgrades, selected crafting outcomes, and bot hunting-area choices use
nonnegative integer weights. For a positive total, an outcome's probability is
its weight divided by the sum of all weights; a zero-weight outcome cannot win.
If every weight is zero, selection falls back to equal probabilities across
the available outcomes. A negative weight is invalid configuration.

Large custom weights are summed without signed 32-bit overflow. Ordinary
configurations retain integer rolls over the same distribution. Lazy item and
weight sources are read once, so computing their contents cannot change the
distribution midway through selection.
