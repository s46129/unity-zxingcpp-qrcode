# Parses the program header table directly so the check needs no readelf on PATH.

cmake_minimum_required(VERSION 3.21)

if(NOT DEFINED ELF_FILE)
    message(FATAL_ERROR "Pass -DELF_FILE=<path to the shared object>.")
endif()

if(NOT DEFINED REQUIRED_ALIGNMENT)
    set(REQUIRED_ALIGNMENT 16384)
endif()

if(NOT EXISTS "${ELF_FILE}")
    message(FATAL_ERROR "ELF alignment check: ${ELF_FILE} does not exist.")
endif()

function(read_little_endian_uint offset size out_variable)
    file(READ "${ELF_FILE}" hex OFFSET ${offset} LIMIT ${size} HEX)
    string(LENGTH "${hex}" hex_length)
    math(EXPR expected_length "${size} * 2")
    if(NOT hex_length EQUAL expected_length)
        message(FATAL_ERROR "ELF alignment check: ${ELF_FILE} is truncated at offset ${offset}.")
    endif()

    set(big_endian_hex "")
    math(EXPR last_byte "${size} - 1")
    foreach(byte_index RANGE ${last_byte})
        math(EXPR hex_start "(${last_byte} - ${byte_index}) * 2")
        string(SUBSTRING "${hex}" ${hex_start} 2 byte)
        string(APPEND big_endian_hex "${byte}")
    endforeach()

    math(EXPR value "0x${big_endian_hex}" OUTPUT_FORMAT DECIMAL)
    set(${out_variable} "${value}" PARENT_SCOPE)
endfunction()

file(READ "${ELF_FILE}" identification OFFSET 0 LIMIT 6 HEX)
if(NOT identification MATCHES "^7f454c46")
    message(FATAL_ERROR "ELF alignment check: ${ELF_FILE} is not an ELF file.")
endif()

string(SUBSTRING "${identification}" 8 2 elf_class)
string(SUBSTRING "${identification}" 10 2 elf_data)
if(NOT elf_class STREQUAL "02" OR NOT elf_data STREQUAL "01")
    message(FATAL_ERROR "ELF alignment check: ${ELF_FILE} is not a little-endian 64-bit ELF file.")
endif()

read_little_endian_uint(32 8 program_header_offset)
read_little_endian_uint(54 2 program_header_size)
read_little_endian_uint(56 2 program_header_count)

if(program_header_count EQUAL 0)
    message(FATAL_ERROR "ELF alignment check: ${ELF_FILE} has no program header table.")
endif()

if(program_header_size LESS 56)
    message(FATAL_ERROR "ELF alignment check: ${ELF_FILE} has ${program_header_size}-byte program headers, too small to hold p_align.")
endif()

set(load_segment_count 0)
set(misaligned_segments "")
math(EXPR last_header "${program_header_count} - 1")
foreach(header_index RANGE ${last_header})
    math(EXPR header_offset "${program_header_offset} + ${header_index} * ${program_header_size}")
    read_little_endian_uint(${header_offset} 4 segment_type)
    if(NOT segment_type EQUAL 1) # PT_LOAD
        continue()
    endif()

    math(EXPR alignment_offset "${header_offset} + 48")
    read_little_endian_uint(${alignment_offset} 8 segment_alignment)
    math(EXPR load_segment_count "${load_segment_count} + 1")
    if(segment_alignment LESS REQUIRED_ALIGNMENT)
        list(APPEND misaligned_segments "PT_LOAD at program header ${header_index} align=${segment_alignment}")
    endif()
endforeach()

if(load_segment_count EQUAL 0)
    message(FATAL_ERROR "ELF alignment check: ${ELF_FILE} has no PT_LOAD segment.")
endif()

if(misaligned_segments)
    string(REPLACE ";" ", " misaligned_text "${misaligned_segments}")
    message(FATAL_ERROR
        "ELF alignment check failed: ${ELF_FILE} must align every PT_LOAD segment to ${REQUIRED_ALIGNMENT} bytes, got ${misaligned_text}.")
endif()

message(STATUS "ELF alignment check passed: ${load_segment_count} PT_LOAD segments in ${ELF_FILE} are aligned to at least ${REQUIRED_ALIGNMENT} bytes.")
