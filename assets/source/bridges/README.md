# 中间姿态生成记录

工具：内置 imagegen，透明背景模式。图像是本项目的过渡素材，正式六个端点保留原件。以下为当次生成提示；后续归一化和补帧由 `tools/prepare-assets.mjs` 完成。

## bottom-strip.png

参考：idle.png 为起始，bottom.png 为终点。

> Create a transparent animation keyframe sprite strip for the EXACT yellow 3D plush character shown in the two reference images. Reference 1 is the starting upright pose, reference 2 is the final prone/kneeling right-facing pose. Produce THREE SEPARATE in-between poses arranged in THREE EQUAL horizontal cells in one wide image, each character isolated with generous transparent gaps. Do NOT include the endpoint poses. Frame 1: character bends knees deeply, torso starts turning to viewer's right, hands lower forward. Frame 2: kneeling three-quarter right view, round rump lowers, both hands reaching the floor. Frame 3: body nearly horizontal, elbows bending, approaching final pose. All three contact the same floor baseline, heads progressively lower, maintain constant head and body model scale. Preserve EXACT soft golden yellow plush, cream belly, green eyes and dark gray fingertips and toes, short thick limbs and pear-shaped body. Every frame must have coherent anatomy with ONLY TWO ARMS, TWO HANDS, TWO LEGS, TWO EYES. Match reference model, face, neutral small smile, lighting and shading. True alpha transparent background. No text, no labels, no grid, no extra characters, no floor/shadow, no props. High quality clean production animation key poses, not dissolves or double-exposures.

## sit-strip.png

参考：sit_1.png 为起始，sit_2.png 为终点。

> Create a transparent animation keyframe sprite strip for the EXACT yellow plush character in the two reference images. Reference 1 is arms crossed looking slightly displeased; reference 2 is relaxed half-lidded thinking with one hand under chin and other forearm resting below. Produce THREE SEPARATE chronological in-between half-body poses in THREE EQUAL horizontal cells in one wide image, with generous transparent gaps. Frame 1: uncrossing arms, viewer-left hand starting to lift away from opposite forearm. Frame 2: same hand raised midway in front of chest with elbow bending naturally upward, other forearm stays horizontal low. Frame 3: hand approaching the chin, index finger and thumb almost in thinking position, eyes starting to relax. Each frame the same character model, SAME head size, head top and torso base baseline aligned, no camera movement. Retain EXACT golden yellow plush and lighting, cream belly, dark gray hands, green irises and black pupils, no clothing. ONLY TWO HANDS, no duplicate limbs, no ghost images. Preserve identity and large neck taper. Do not include the endpoint poses. Transparent alpha background, no props, no table, no lower legs added, no text or labels or panel dividers.

## peek-strip.png

参考：idle.png 为起始，peek_left.png 为终点。

> Create a transparent animation keyframe sprite strip for the EXACT yellow 3D plush character in the two reference images, transitioning from standing to peeking out from the LEFT screen edge toward the RIGHT. Produce THREE SEPARATE chronological intermediate poses in THREE EQUAL horizontal cells, one pose in each cell, isolated with ample transparent gap. Frame 1: full body turns three-quarter toward right, feet bending and arms moving toward viewer-left, head leans a little right. Frame 2: torso leans right while body is partly hidden behind an imaginary vertical LEFT boundary, one gray hand rests on that edge high and the other low; only upper body visible. Frame 3: near the reference 2 left-edge peeking pose, both gray hands holding the vertical left cut edge, head and torso protruding toward right. The invisible edge is not drawn; all background transparent. Preserve exact character and green eyes, small neutral smile, golden yellow soft plush and cream belly, gray hands and feet, same head/model scale. Keep anatomically coherent two arms and hands, no duplicates or text. Do NOT include endpoint poses. True alpha, no wall or door or object, no floor/shadow or grid.
