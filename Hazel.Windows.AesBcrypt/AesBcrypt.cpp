#include <stdint.h>
#include <memory>

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <bcrypt.h>
#pragma comment(lib, "bcrypt.lib")

#define NT_SUCCESS(status) (static_cast<NTSTATUS>(status) >= 0)

struct AesBcrypt
{
	BCRYPT_ALG_HANDLE m_algorithm = nullptr;
	BCRYPT_KEY_HANDLE m_key = nullptr;

	~AesBcrypt()
	{
		if (nullptr != m_key)
		{
			BCryptDestroyKey(m_key);
		}

		if (nullptr != m_algorithm)
		{
			BCryptCloseAlgorithmProvider(m_algorithm, 0);
		}
	}
};

/**
 * Create a new AesBcrypt context with the specified key
 */
extern "C" __declspec(dllexport) AesBcrypt* AesBrypt_create(uint8_t* keyArray, int keyArrayOffset, int keyArrayLength)
{
	std::unique_ptr<AesBcrypt> context = std::make_unique<AesBcrypt>();

	NTSTATUS result;

	// Validate key size
	if (keyArrayLength != 16 && keyArrayLength != 24 && keyArrayLength != 32)
	{
		return nullptr;
	}

	// Create the AES algorithm
	result = BCryptOpenAlgorithmProvider(
		  &context->m_algorithm
		, BCRYPT_AES_ALGORITHM
		, nullptr
		, 0
	);
	if (!NT_SUCCESS(result))
	{
		return nullptr;
	}

	result = BCryptSetProperty(
		  context->m_algorithm
		, BCRYPT_CHAINING_MODE
		, reinterpret_cast<PBYTE>(const_cast<wchar_t*>(BCRYPT_CHAIN_MODE_ECB))
		, sizeof(BCRYPT_CHAIN_MODE_ECB)
		, 0
	);
	if (!NT_SUCCESS(result))
	{
		return nullptr;
	}

	// Create the symetric key object
	result = BCryptGenerateSymmetricKey(
		  context->m_algorithm
		, &context->m_key
		, nullptr
		, 0
		, keyArray + keyArrayOffset
		, keyArrayLength
		, 0
	);
	if (!NT_SUCCESS(result))
	{
		return nullptr;
	}

	return context.release();
}

/**
 * Destroy the specified AesBcrypt context
 */
extern "C" __declspec(dllexport) void AesBcrypt_release(AesBcrypt * unsafeContext)
{
	std::unique_ptr<AesBcrypt> context(unsafeContext);
	context.reset();
}

/**
 * Encrypt a block with the specified AesBcrypt context
 */
extern "C" __declspec(dllexport) int AesBcrypt_encrpytBlock(AesBcrypt * context, uint8_t * inputArray, int inputOffset, int inputLength, uint8_t * outputArray, int outputOffset)
{
	if (nullptr == context)
	{
		return 0;
	}

	ULONG bytesEncrypted = 0;
	NTSTATUS result = BCryptEncrypt(
		  context->m_key
		, inputArray + inputOffset
		, inputLength
		, nullptr
		, nullptr
		, 0
		, outputArray + outputOffset
		, inputLength
		, &bytesEncrypted
		, 0
	);
	if (!NT_SUCCESS(result))
	{
		return 0;
	}

	return bytesEncrypted;
}
