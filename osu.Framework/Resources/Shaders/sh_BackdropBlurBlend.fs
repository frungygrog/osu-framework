#ifndef BLUR_FS
#define BLUR_FS

#include "sh_Utils.h"
#include "sh_Masking.h"
#include "sh_TextureWrapping.h"

#undef INV_SQRT_2PI
#define INV_SQRT_2PI 0.39894

layout(location = 2) in mediump vec2 v_TexCoord;

layout(std140, set = 0, binding = 0) uniform m_BlendParameters
{
	lowp float g_MaskCutoff;
	lowp float g_BackdropOpacity;
	lowp float g_BackdropTintStrength;
};

layout(set = 1, binding = 0) uniform lowp texture2D m_Texture;
layout(set = 1, binding = 1) uniform lowp sampler m_Sampler;

layout(set = 2, binding = 0) uniform lowp texture2D m_Mask;
layout(set = 2, binding = 1) uniform lowp sampler m_MaskSampler;

layout(set = 3, binding = 0) uniform lowp texture2D m_GradientTexture;
layout(set = 3, binding = 1) uniform lowp sampler m_GradientSampler;

layout(std140, set = 4, binding = 0) uniform m_PathTextureParameters
{
	highp vec4 TexRect1;
};

layout(location = 0) out vec4 o_Colour;

void main(void)
{
	vec2 wrappedCoord = wrap(v_TexCoord, v_TexRect);

	vec4 mask = wrappedSampler(wrappedCoord, v_TexRect, m_Mask, m_MaskSampler, -0.9);

	// Compute colored foreground using the gradient texture and the mask's distance field (r) and alpha (a).
	lowp vec4 pathCol = texture(sampler2D(m_GradientTexture, m_GradientSampler), TexRect1.xy + vec2(mask.r, 0.0) * TexRect1.zw, -0.9);
	
	// Calculate Premultiplied Foreground.
	// pathCol.rgb is Straight. v_Colour.rgb is Premultiplied.
	// pathCol.a * mask.a is the geometry alpha.
	// Result = (PathStraight * V_Premul) * GeomAlpha.
	// Multiply by 0.9 to slightly lower the opacity of the slider body path as requested.
	vec4 foreground = vec4(pathCol.rgb * v_Colour.rgb, v_Colour.a) * (pathCol.a * mask.a * 0.9);

	// Calculate Straight Foreground for blending equations.
	vec3 fg_straight = vec3(0.0);
	if (foreground.a > 0.00001)
		fg_straight = foreground.rgb / foreground.a;

	if (mask.a > g_MaskCutoff) {
		vec4 background = wrappedSampler(wrappedCoord, v_TexRect, m_Texture, m_Sampler, -0.9) * g_BackdropOpacity;

		if (background.a > 0.0) {
			// background.rgb is Premultiplied here.
			// We mix Straight Background with Straight Foreground, then re-multiply by background.a.
			// background.rgb / background.a -> Straight BG.
			// fg_straight -> Straight FG.
			background.rgb = mix(background.rgb / background.a, background.rgb / background.a * fg_straight, g_BackdropTintStrength * foreground.a) * background.a;

			float alpha = background.a + (1.0 - background.a) * foreground.a;
			
			// mix(PremulBG, StraightFG, fg.a) -> PremulBG * (1-fg.a) + StraightFG * fg.a = PremulBG * (1-fg.a) + PremulFG.
			// Result is Premul Total. 
			// We divide by alpha to return Straight Total, as expected by the vec4 construct.
			o_Colour = vec4(mix(background.rgb, fg_straight, foreground.a) / alpha, alpha);
		} else {
			o_Colour = vec4(fg_straight, foreground.a);
		}

	} else {
		o_Colour = vec4(fg_straight, foreground.a);
	}
}

#endif
