#ifndef POTION_LIQUID_BUBBLE_EFFECTS_INCLUDED
#define POTION_LIQUID_BUBBLE_EFFECTS_INCLUDED

// Supplied per renderer by LiquidBubbleController. Not material/global state.
float _LiquidBubbleCount;
float4 _LiquidBubbleData[24];       // gravity-aligned center.xyz, radius
float4 _LiquidBubbleShape[24];      // surface flag, vertical scale, age, reserved
float _LiquidRippleCount;
float4 _LiquidRippleData[16];       // center.xz, birth time, source radius
float _LiquidBubbleTime;
float4 _LiquidBubbleSettings;       // ripple lifetime, speed, normal strength, internal visibility

float3 LiquidBubbleLightDirection()
{
#if defined(SHADERGRAPH_PREVIEW)
    return normalize(float3(-0.4, 0.8, -0.5));
#else
    // Main light data is also available to an Unlit URP graph. Preserve the
    // original liquid art direction and light only the new curved details.
    float3 direction = _MainLightPosition.xyz;
    return dot(direction, direction) > 0.001 ? normalize(direction)
        : normalize(float3(-0.4, 0.8, -0.5));
#endif
}

float3 LiquidBubbleShade(float3 liquid, float3 normal, float3 view, float depth,
    float surface, float opacity)
{
    float3 light = LiquidBubbleLightDirection();
    float facing = saturate(dot(normal, view));
    float fresnel = pow(1.0 - facing, 3.0);
    float diffuse = saturate(dot(normal, light));
    float3 halfVector = normalize(light + view + float3(0.00001, 0, 0));
    float specular = pow(saturate(dot(normal, halfVector)), 96.0);
    float broadReflection = pow(saturate(dot(normal, halfVector)), 12.0);
    float absorption = exp(-max(depth, 0.0) * 1.6);
    float visibility = lerp(_LiquidBubbleSettings.w * absorption, 1.0, surface);
    float3 liquidTint = liquid / max(max(liquid.r, max(liquid.g, liquid.b)), 1.0);
    // The submerged shell picks up more of the liquid color and loses the
    // sharp air-side reflection. Its existing visibility setting controls how
    // clearly the lower hemisphere can be seen through the liquid.
    float3 tint = lerp(_BubbleColor.rgb, liquidTint, lerp(0.65, 0.22, surface));
    float3 shell = tint * (0.38 + diffuse * 0.42 + fresnel * 0.65);
    shell += float3(1.0, 1.0, 0.92) * (specular * 1.7 + broadReflection * 0.16)
        * lerp(0.35, 1.0, surface);
    // A dark curved underside and a bright grazing rim make the sphere legible
    // without filling the entire bubble with an opaque white disk.
    float coverage = (0.3 + surface * 0.35 + fresnel * 0.5 + specular * 0.4)
        * visibility * opacity;
    return lerp(liquid, shell, saturate(coverage));
}

void LiquidFoam_float(float3 BaseColor, float3 FoamColor, float FoamWidth,
    float FoamStrength, float GradientNoise, float Depth, float Motion,
    float DetailNoise, float SceneDepthRaw, float3 WorldPosition, float FoamCutoff,
    bool IsFront, out float3 Color, out float Foam)
{
    // Preserve the original foam/contact placement.
    float sceneEyeDepth = unity_OrthoParams.w > 0.5
        ? lerp(_ProjectionParams.y, _ProjectionParams.z,
#if UNITY_REVERSED_Z
            1.0 - SceneDepthRaw
#else
            SceneDepthRaw
#endif
          ) : LinearEyeDepth(SceneDepthRaw, _ZBufferParams);
    float surfaceEyeDepth = -TransformWorldToView(WorldPosition).z;
    float contactDepth = max(sceneEyeDepth - surfaceEyeDepth, 0.0);
    float width = max(FoamWidth, 0.00001) * (1.0 + Motion * 2.0);
    float depthFade = max(max(Depth, 0.0) / width, contactDepth / width);
    float cutoff = max(depthFade * max(FoamCutoff, 0.0001), 0.0001);
    float mask = smoothstep(cutoff * 0.8, cutoff, GradientNoise);
    Foam = saturate(mask * sqrt(saturate(DetailNoise)) * saturate(FoamStrength))
        * step(0.0, Depth) * step(0.00001, FoamWidth);
    Color = lerp(BaseColor, FoamColor, Foam);

    float opacity = saturate(_BubbleColor.a) * step(0.00001, _BubbleSize)
        * step(0.00001, _BubbleAmount);
    if (opacity <= 0.0) return;

    float3 origin = TransformObjectToWorld(float3(0, 0, 0));
    float heightScale = max(length(TransformObjectToWorldDir(float3(0, 1, 0), false)), 0.0001);
    float3 fragment = (WorldPosition - origin) / heightScale;
    float3 rayDirection = -GetWorldSpaceNormalizeViewDir(WorldPosition);
    float3 rayOrigin = unity_OrthoParams.w > 0.5 ? fragment - rayDirection * 100.0
        : (_WorldSpaceCameraPos - origin) / heightScale;
    float2 tilt = float2(_WobbleX, _WobbleZ);
    float level = _Amount - 0.5;
    float planeDenominator = rayDirection.y - dot(rayDirection.xz, tilt);
    float planeDistance = (level - rayOrigin.y + dot(rayOrigin.xz, tilt))
        / (abs(planeDenominator) > 0.00001 ? planeDenominator : 0.00001);
    float fragmentDistance = dot(fragment - rayOrigin, rayDirection);
    bool isTop = !IsFront && planeDistance > 0.0 && abs(planeDenominator) > 0.00001;
    float entryDistance = isTop ? planeDistance : fragmentDistance;
    float3 surfacePoint = rayOrigin + rayDirection * entryDistance;
    float3 planeNormal = normalize(float3(-tilt.x, 1.0, -tilt.y));

    // Expanding, damped wave packets. Their analytic derivative perturbs the
    // surface normal; the rings therefore move reflected light, not just color.
    float2 slope = 0.0;
    float crest = 0.0;
    float pixelWidth = max(length(fwidth(surfacePoint.xz)), 0.0003);
    if (isTop)
    {
        [loop] for (int j = 0; j < min((int)_LiquidRippleCount, 16); j++)
        {
            float4 ripple = _LiquidRippleData[j];
            float age = _LiquidBubbleTime - ripple.z;
            float lifetime = max(_LiquidBubbleSettings.x, 0.01);
            if (age < 0.0 || age >= lifetime) continue;
            float2 offset = surfacePoint.xz - ripple.xy;
            float distance = length(offset);
            float band = max(ripple.w * 0.42, pixelWidth * 2.0);
            float ringRadius = ripple.w + age * _LiquidBubbleSettings.y;
            float q = (distance - ringRadius) / band;
            float envelope = exp(-q * q * 0.7);
            float fade = pow(saturate(1.0 - age / lifetime), 2.0);
            float phase = q * 3.4;
            float derivative = envelope * (3.4 * cos(phase) - 1.4 * q * sin(phase));
            slope += offset / max(distance, 0.0001) * derivative
                * fade * _LiquidBubbleSettings.z * 2.5;
            crest += envelope * cos(phase) * fade;
        }
        float3 normal = normalize(planeNormal - float3(slope.x, 0.0, slope.y));
        float3 light = LiquidBubbleLightDirection();
        float3 view = -rayDirection;
        float3 halfVector = normalize(light + view + float3(0.00001, 0, 0));
        float deltaDiffuse = dot(normal, light) - dot(planeNormal, light);
        float deltaSpecular = pow(saturate(dot(normal, halfVector)), 64.0)
            - pow(saturate(dot(planeNormal, halfVector)), 64.0);
        Color = max(Color * (1.0 + deltaDiffuse * 1.3
            - saturate(-crest) * 0.16 * opacity) + deltaSpecular * 0.65
            + crest * 0.035 * opacity, 0.0);
    }

    // Analytic ray/ellipsoid intersections give each rising bubble a real 3D
    // normal and camera-dependent silhouette inside the existing liquid mask.
    float nearest = 1e20;
    float3 hitNormal = planeNormal;
    float hitSurface = 0.0;
    float hitDepth = 0.0;
    [loop] for (int i = 0; i < min((int)_LiquidBubbleCount, 24); i++)
    {
        float4 bubble = _LiquidBubbleData[i];
        float4 shape = _LiquidBubbleShape[i];
        if (bubble.w < 0.0001) continue;
        float waterHeight = level + dot(bubble.xz, tilt);
        float centerDepth = waterHeight - bubble.y;
        float approach = 1.0 - smoothstep(bubble.w * 0.25, bubble.w * 1.8,
            max(centerDepth, 0.0));
        // Center the resting sphere on the plane: one hemisphere stays in the
        // liquid. Ease out the simulation's former quarter-radius cap offset
        // during ascent so reaching the surface doesn't cause a position jump.
        bubble.y = shape.x > 0.5 ? waterHeight : bubble.y + bubble.w * 0.25 * approach;
        float3 axes = bubble.w * float3(1.0, max(shape.y, 0.2), 1.0);
        float3 relative = (rayOrigin - bubble.xyz) / axes;
        float3 direction = rayDirection / axes;
        float a = dot(direction, direction);
        float b = dot(relative, direction);
        float discriminant = b * b - a * (dot(relative, relative) - 1.0);
        if (discriminant <= 0.0) continue;
        float distance = (-b - sqrt(discriminant)) / a;
        float allowance = isTop && approach > 0.0
            ? bubble.w * 2.0 / max(abs(dot(rayDirection, planeNormal)), 0.15) : 0.001;
        if (distance <= 0.0 || distance < entryDistance - allowance || distance >= nearest) continue;
        float3 hit = rayOrigin + rayDirection * distance;
        nearest = distance;
        hitNormal = normalize((hit - bubble.xyz) / (axes * axes));
        // Classify the ray hit, not the bubble's lifetime or the liquid face.
        // This follows the tilted water plane rather than splitting the screen
        // circle in half. A narrow transition softens the meniscus boundary.
        float signedHeight = dot(hit - float3(0.0, level, 0.0), planeNormal);
        float waterlineWidth = max(bubble.w * 0.06, pixelWidth * 0.65);
        hitSurface = smoothstep(-waterlineWidth, waterlineWidth, signedHeight);
        hitDepth = max(distance - entryDistance, 0.0);
    }
    if (nearest < 1e19)
        Color = LiquidBubbleShade(Color, hitNormal, -rayDirection, hitDepth, hitSurface, opacity);
}

#endif
