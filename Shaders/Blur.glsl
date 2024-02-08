SHADER FRAG Blur
	PASS	
		#version 410

		layout(location = 0) out vec4 color;

		uniform sampler2D MainTex;
		uniform vec2 MainTex_Size;

		void main()
		{
			vec3 col = vec3(0);
			int size = 32;
			int halfSize = int(size * 0.5f);
			for (int i = -halfSize; i <= halfSize; i++)
			{
				vec2 pos = (gl_FragCoord.xy + vec2(0, i)) / MainTex_Size;
				col += texture(MainTex, pos).xyz;
			}
			col /= size;
			color = vec4(col, 0);
		}
	PASS
		#version 410

		layout(location = 0) out vec4 color;

		uniform sampler2D MainTex;
		uniform vec2 MainTex_Size;

		void main()
		{
			vec3 col = vec3(0);
			int size = 32;
			int halfSize = int(size * 0.5f);
			for (int i = -halfSize; i <= halfSize; i++)
			{
				vec2 pos = (gl_FragCoord.xy + vec2(i, 0)) / MainTex_Size;
				col += texture(MainTex, pos).xyz;
			}
			col /= size;
			color = vec4(col, 0);
		}