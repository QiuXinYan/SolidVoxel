#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

#include "Surface.hlsl"

float3 GetLighting (Surface surface, Light light) {
	return IncomingLight(surface, light) * surface.color;
}

float3 GetLighting (Surface surface) {
	return GetLighting(surface, GetDirectionalLight());
}

#endif